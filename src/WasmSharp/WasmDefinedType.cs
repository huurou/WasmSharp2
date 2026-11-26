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
            var structure = new StructureReader(member);
            hash.Add(structure.Header);
            while (structure.TryRead(out var value, out var storage))
            {
                if (storage != default)
                {
                    hash.Add(storage);
                }
                member.AccumulateValue(value, ref hash, ref missing, ref valid);
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
        return declaration.IsStruct
            ? new WasmStructType(group, position, declaration.IsFinal)
            : new WasmFunctionType(group, position, declaration.IsFinal);
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
                var first = new StructureReader(a);
                var second = new StructureReader(b);
                if (first.Header != second.Header)
                {
                    return false;
                }
                while (first.TryRead(out var leftValue, out var leftStorage))
                {
                    // 各宣言の要素数が一致するため、両方から同じ順序で同じ個数を読み取れる。
                    second.TryRead(out var rightValue, out var rightStorage);
                    if (leftStorage != rightStorage || !CompareValue(a, b, leftValue, rightValue))
                    {
                        return false;
                    }
                }
            }
            if (firstPending is null && additionalPending is { Count: > 0 })
            {
                firstPending = additionalPending.Pop();
            }
        }
        return true;

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

    /// <summary>
    /// 定義の各型列を区別し、上位型、引数、結果、フィールドを宣言順に読み取る
    /// </summary>
    private ref struct StructureReader
    {
        private ReadOnlySpan<WasmValueType> _supertypes;
        private ReadOnlySpan<WasmValueType> _parameters;
        private ReadOnlySpan<WasmValueType> _results;
        private ReadOnlySpan<WasmFieldType> _fields;

        internal (bool IsFinal, WasmValueKind Kind, int Supertypes, int Parameters, int Results, int Fields) Header { get; }

        internal StructureReader(WasmDefinedType type)
        {
            _supertypes = type._supertypes.AsSpan();
            if (type is WasmFunctionType function)
            {
                _parameters = function.ParameterTypes.AsSpan();
                _results = function.ResultTypes.AsSpan();
            }
            else
            {
                _fields = ((WasmStructType)type).Fields.AsSpan();
            }
            Header = (
                type.IsFinal,
                type.ReferenceKind,
                _supertypes.Length,
                _parameters.Length,
                _results.Length,
                _fields.Length
            );
        }

        internal bool TryRead(out WasmValueType value, out (bool IsMutable, WasmPackedType PackedType) storage)
        {
            storage = default;
            if (Take(ref _supertypes, out value) || Take(ref _parameters, out value) || Take(ref _results, out value))
            {
                return true;
            }
            if (Take(ref _fields, out var field))
            {
                value = field.ValueType;
                storage = (field.IsMutable, field.PackedType);
                return true;
            }
            return false;
        }

        private static bool Take<T>(ref ReadOnlySpan<T> remaining, out T value)
            where T : struct
        {
            if (remaining.IsEmpty)
            {
                value = default;
                return false;
            }
            value = remaining[0];
            remaining = remaining[1..];
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
