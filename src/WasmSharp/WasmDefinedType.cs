using System.Collections.Immutable;
using WasmSharp.Modules.Definitions;

namespace WasmSharp;

/// <summary>
/// 再帰グループ内の位置と構造で識別するWasmの定義型
/// </summary>
public abstract class WasmDefinedType
{
    private readonly WasmDefinedType[]? _group;
    private readonly int _position;
    private ImmutableArray<WasmValueType> _supertypes = [];
    private bool _isGroupValid = true;

    private protected const int MaximumDisplayedValueTypes = 16;
    private bool IsFinal { get; }
    private int GroupSize => _group?.Length ?? 1;
    internal SupertypeError SupertypeError { get; private set; }
    internal WasmValueType.UnresolvedReference? Unresolved { get; private set; }
    internal int StructuralHashCode { get; private set; }
    internal abstract WasmValueKind ReferenceKind { get; }

    private protected WasmDefinedType(WasmDefinedType[]? group, int position, bool isFinal)
    {
        _group = group;
        _position = position;
        IsFinal = isFinal;
    }

    private protected static void CompleteGroup(ReadOnlySpan<WasmDefinedType> members)
    {
        var hash = new HashCode();
        WasmValueType.UnresolvedReference? missing = null;
        var valid = true;
        foreach (var member in members)
        {
            hash.Add(member.IsFinal);
            hash.Add(member.ReferenceKind);
            member.AccumulateHashAndReferenceState(member._supertypes, ref hash, ref missing, ref valid);
            if (member is WasmFunctionType function)
            {
                member.AccumulateHashAndReferenceState(function.ParameterTypes, ref hash, ref missing, ref valid);
                member.AccumulateHashAndReferenceState(function.ResultTypes, ref hash, ref missing, ref valid);
            }
            else if (member is WasmStructType structure)
            {
                hash.Add(structure.Fields.Length);
                foreach (var field in structure.Fields)
                {
                    hash.Add(field.IsMutable);
                    hash.Add(field.PackedType);
                    member.AccumulateValue(field.ValueType, ref hash, ref missing, ref valid);
                }
            }
        }
        var groupHash = hash.ToHashCode();
        foreach (var member in members)
        {
            member.StructuralHashCode = HashCode.Combine(groupHash, member._position);
            member.Unresolved = missing;
        }

        // すべての投影と上位型を参照できる状態で照合し、グループ全体の結果を最後に反映する。
        foreach (var member in members)
        {
            member.SupertypeError = member.CheckSupertype();
            valid &= member.SupertypeError == SupertypeError.None;
        }
        if (!valid || missing is not null)
        {
            foreach (var member in members)
            {
                member._isGroupValid = false;
            }
        }
    }

    private SupertypeError CheckSupertype()
    {
        if (_supertypes.Length > 1)
        {
            return SupertypeError.Multiple;
        }
        if (_supertypes is not [{ DefinedType: { } parent }])
        {
            return SupertypeError.None;
        }
        if (!IsSupertypeOrderValid(parent))
        {
            return SupertypeError.Forward;
        }
        if (parent.IsFinal)
        {
            return SupertypeError.Final;
        }
        return MatchesSupertype(parent) ? SupertypeError.None : SupertypeError.Structure;
    }

    /// <summary>
    /// 値型列を一度たどり、構造ハッシュ、最初の未解決参照、参照先グループの妥当性を集計する
    /// </summary>
    private void AccumulateHashAndReferenceState(
        ImmutableArray<WasmValueType> values,
        ref HashCode hash,
        ref WasmValueType.UnresolvedReference? missing,
        ref bool valid
    )
    {
        hash.Add(values.Length);
        foreach (var value in values)
        {
            AccumulateValue(value, ref hash, ref missing, ref valid);
        }
    }

    private void AccumulateValue(
        WasmValueType value,
        ref HashCode hash,
        ref WasmValueType.UnresolvedReference? missing,
        ref bool valid
    )
    {
        if (value.DefinedType is { } target && IsInSameGroup(target))
        {
            // グループ内の参照は投影位置で表し、参照先を再展開しない。
            hash.Add(value.IsNullable);
            hash.Add(target._position);
        }
        else
        {
            hash.Add(value);
            missing ??= value.Unresolved;
            valid &= value.DefinedType?._isGroupValid != false;
        }
    }

    private bool IsInSameGroup(WasmDefinedType other)
    {
        return ReferenceEquals(this, other) || (_group is not null && ReferenceEquals(_group, other._group));
    }

    private bool IsSupertypeOrderValid(WasmDefinedType parent)
    {
        // グループ外の参照先は先行グループに属する。同じグループでは先行する宣言だけを上位型にできる。
        return !IsInSameGroup(parent) || parent._position < _position;
    }

    private WasmDefinedType GetGroupMember(int position)
    {
        return _group?[position] ?? this;
    }

    /// <summary>
    /// 再帰グループ内の型番号を、そのグループの定義型への参照に置き換える
    /// </summary>
    internal static WasmDefinedType FromDecodedGroup(ReadOnlySpan<TypeDeclaration> declarations, int firstIndex)
    {
        if (declarations.Length == 1)
        {
            var type = CreateMember(declarations[0], null, 0);
            type.SetDecodedDeclaration(declarations[0], firstIndex, [type]);
            CompleteGroup([type]);
            return type;
        }

        // すべての参照先を確保してから、個々の型の内容を確定する。
        var members = new WasmDefinedType[declarations.Length];
        for (var index = 0; index < members.Length; index++)
        {
            members[index] = CreateMember(declarations[index], members, index);
        }
        for (var index = 0; index < members.Length; index++)
        {
            members[index].SetDecodedDeclaration(declarations[index], firstIndex, members);
        }
        CompleteGroup(members);
        return members[0];
    }

    private static WasmDefinedType CreateMember(TypeDeclaration declaration, WasmDefinedType[]? group, int position)
    {
        return declaration.Fields.IsDefault
            ? new WasmFunctionType(group, position, declaration.IsFinal)
            : new WasmStructType(group, position, declaration.IsFinal);
    }

    internal void AppendGroupTo(List<WasmDefinedType> types)
    {
        if (_group is null)
        {
            types.Add(this);
        }
        else
        {
            types.AddRange(_group);
        }
    }

    private void SetDecodedDeclaration(TypeDeclaration declaration, int firstIndex, ReadOnlySpan<WasmDefinedType> members)
    {
        _supertypes = ResolveReferences(declaration.Supertypes, firstIndex, members);
        SetDecodedComposite(declaration, firstIndex, members);
    }

    private protected static ImmutableArray<WasmValueType> ResolveReferences(
        ImmutableArray<WasmValueType> values,
        int firstIndex,
        ReadOnlySpan<WasmDefinedType> members
    )
    {
        ImmutableArray<WasmValueType>.Builder? resolved = null;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (ResolveReference(value, firstIndex, members) is { } newType)
            {
                resolved ??= values.ToBuilder();
                resolved[index] = newType;
            }
        }
        return resolved?.MoveToImmutable() ?? values;
    }

    private protected static WasmValueType? ResolveReference(
        WasmValueType value,
        int firstIndex,
        ReadOnlySpan<WasmDefinedType> members
    )
    {
        if (value.DefinedType is null && value.Unresolved is { } reference)
        {
            var position = reference.Index - (uint)firstIndex;
            if (position < (uint)members.Length)
            {
                return WasmValueType.DefinedReference(members[(int)position], value.IsNullable);
            }
        }
        return null;
    }

    private protected abstract void SetDecodedComposite(
        TypeDeclaration declaration,
        int firstIndex,
        ReadOnlySpan<WasmDefinedType> members
    );

    private protected abstract bool MatchesSupertype(WasmDefinedType parent);

    /// <summary>
    /// モジュールの型番号に依存せず、参照先を含めた定義型の同値性を判定する
    /// </summary>
    public bool IsEquivalentTo(WasmDefinedType other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (StructuralHashCode != other.StructuralHashCode)
        {
            return false;
        }
        return HasSameStructure(other);
    }

    /// <summary>
    /// 指定した型と同値か、宣言した上位型を通じてその型に代入できるかを返す
    /// </summary>
    internal bool IsSubtypeOf(WasmDefinedType expected)
    {
        var current = this;
        while (!current.IsEquivalentTo(expected))
        {
            if (
                !current._isGroupValid
                || current._supertypes is not [{ DefinedType: { } supertype }]
                || !current.IsSupertypeOrderValid(supertype)
            )
            {
                return false;
            }
            current = supertype;
        }
        return true;
    }

    /// <summary>
    /// 投影位置とグループの全宣言を照合し、先行グループへの参照を順にたどる
    /// </summary>
    internal bool HasSameStructure(WasmDefinedType other)
    {
        if (_position != other._position)
        {
            return false;
        }
        (WasmDefinedType Left, WasmDefinedType Right)? firstPending = (this, other);
        Stack<(WasmDefinedType Left, WasmDefinedType Right)>? additionalPending = null;
        HashSet<(WasmDefinedType Left, WasmDefinedType Right)>? visited = null;
        while (firstPending is { } current)
        {
            firstPending = null;
            var (left, right) = current;
            var size = left.GroupSize;
            if (size != right.GroupSize)
            {
                return false;
            }
            for (var index = 0; index < size; index++)
            {
                var a = left.GetGroupMember(index);
                var b = right.GetGroupMember(index);
                if (a.IsFinal != b.IsFinal || !CompareValues(a, b, a._supertypes, b._supertypes) || !CompareComposite(a, b))
                {
                    return false;
                }
            }
            if (firstPending is null && additionalPending is { Count: > 0 })
            {
                firstPending = additionalPending.Pop();
            }
        }
        return true;

        bool CompareComposite(WasmDefinedType a, WasmDefinedType b)
        {
            if (a is WasmFunctionType f && b is WasmFunctionType g)
            {
                return CompareValues(a, b, f.ParameterTypes, g.ParameterTypes)
                    && CompareValues(a, b, f.ResultTypes, g.ResultTypes);
            }
            if (a is not WasmStructType s || b is not WasmStructType t || s.Fields.Length != t.Fields.Length)
            {
                return false;
            }
            for (var index = 0; index < s.Fields.Length; index++)
            {
                var left = s.Fields[index];
                var right = t.Fields[index];
                if (
                    left.IsMutable != right.IsMutable
                    || left.PackedType != right.PackedType
                    || !CompareValue(a, b, left.ValueType, right.ValueType)
                )
                {
                    return false;
                }
            }
            return true;
        }

        bool CompareValues(
            WasmDefinedType leftOwner,
            WasmDefinedType rightOwner,
            ImmutableArray<WasmValueType> first,
            ImmutableArray<WasmValueType> second
        )
        {
            if (first.Length != second.Length)
            {
                return false;
            }
            for (var index = 0; index < first.Length; index++)
            {
                if (!CompareValue(leftOwner, rightOwner, first[index], second[index]))
                {
                    return false;
                }
            }
            return true;
        }

        bool CompareValue(WasmDefinedType leftOwner, WasmDefinedType rightOwner, WasmValueType a, WasmValueType b)
        {
            if (!a.HasSameForm(b))
            {
                return false;
            }
            if (a.DefinedType is { } nested)
            {
                var otherTarget = b.DefinedType!;
                var local = leftOwner.IsInSameGroup(nested);
                if (nested._position != otherTarget._position || local != rightOwner.IsInSameGroup(otherTarget))
                {
                    return false;
                }
                if (local || ReferenceEquals(nested, otherTarget))
                {
                    return true;
                }
                // 投影位置を確認した後は、外側のグループの定義全体を一度だけ比較する。
                var pair = (nested.GetGroupMember(0), otherTarget.GetGroupMember(0));
                if ((firstPending is { } pending && pending == pair) || (visited is not null && !visited.Add(pair)))
                {
                    return true;
                }
                if (firstPending is not { } firstPair)
                {
                    firstPending = pair;
                }
                else
                {
                    // 異なる参照先に分岐したときだけ、保留中の組と照合済みの組をコレクションで管理する。
                    if (additionalPending is null)
                    {
                        additionalPending = new Stack<(WasmDefinedType, WasmDefinedType)>();
                        visited = [firstPair, pair];
                    }
                    additionalPending.Push(pair);
                }
            }
            return true;
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var remaining = MaximumDisplayedValueTypes;
        return Describe(ref remaining, []);
    }

    private string Describe(ref int remaining, ImmutableStack<WasmDefinedType> ancestors)
    {
        // 表示中の型に戻る参照は、その型の再帰グループ内の位置で示す。
        foreach (var ancestor in ancestors)
        {
            if (ReferenceEquals(ancestor, this))
            {
                return $"(rec {_position})";
            }
        }
        return DescribeComposite(ref remaining, ancestors.Push(this));
    }

    private protected abstract string DescribeComposite(ref int remaining, ImmutableStack<WasmDefinedType> path);

    private protected static string DescribeValue(
        WasmValueType value,
        ref int remaining,
        ImmutableStack<WasmDefinedType> path
    )
    {
        remaining--;
        return value.Describe(value.DefinedType?.Describe(ref remaining, path));
    }
}
