using System.Collections.Immutable;
using System.Text;
using WasmSharp.Modules.Definitions;

namespace WasmSharp;

/// <summary>
/// functionの型を表現するクラス
/// </summary>
public sealed class WasmFunctionType
{
    private const int MaximumDisplayedValueTypes = 16;
    private readonly WasmFunctionType[]? _group;
    private readonly int _position;
    private ImmutableArray<WasmValueType> _supertypes = [];
    private bool _isGroupValid = true;

    private bool IsFinal { get; } = true;

    private int GroupSize => _group?.Length ?? 1;

    internal FunctionSupertypeError SupertypeError { get; private set; }

    /// <summary>
    /// null可否と参照先の関数型を含む引数型
    /// </summary>
    public ImmutableArray<WasmValueType> ParameterTypes { get; private set; }

    /// <summary>
    /// null可否と参照先の関数型を含む結果型
    /// </summary>
    public ImmutableArray<WasmValueType> ResultTypes { get; private set; }

    internal WasmValueType.UnresolvedReference? Unresolved { get; private set; }

    internal int StructuralHashCode { get; private set; }

    /// <summary>
    /// 引数の型を保持する不変配列
    /// </summary>
    public ImmutableArray<WasmValueKind> Parameters { get; private set; }

    /// <summary>
    /// 戻り値の型を保持する不変配列
    /// </summary>
    public ImmutableArray<WasmValueKind> Results { get; private set; }

    /// <summary>
    /// 引数と戻り値の型をコピーして構築する
    /// </summary>
    /// <param name="parameters">宣言順に並べた引数の型</param>
    /// <param name="results">宣言順に並べた戻り値の型</param>
    /// <exception cref="ArgumentOutOfRangeException">Core 2.0で定義されていない値型を含む場合</exception>
    public WasmFunctionType(ReadOnlySpan<WasmValueKind> parameters, ReadOnlySpan<WasmValueKind> results)
        : this(ConvertKinds(parameters, nameof(parameters)), ConvertKinds(results, nameof(results))) { }

    private WasmFunctionType(ImmutableArray<WasmValueType> parameters, ImmutableArray<WasmValueType> results)
    {
        SetSignature(parameters, results);
        CompleteGroup([this]);
    }

    private WasmFunctionType(WasmFunctionType[]? group, int position, bool isFinal)
    {
        _group = group;
        _position = position;
        IsFinal = isFinal;
    }

    private void SetSignature(ImmutableArray<WasmValueType> parameters, ImmutableArray<WasmValueType> results)
    {
        ParameterTypes = parameters;
        ResultTypes = results;
        Parameters = ImmutableArray.CreateRange(parameters, static x => x.Kind);
        Results = ImmutableArray.CreateRange(results, static x => x.Kind);
    }

    private static void CompleteGroup(ReadOnlySpan<WasmFunctionType> members)
    {
        var hash = new HashCode();
        WasmValueType.UnresolvedReference? missing = null;
        var valid = true;
        foreach (var member in members)
        {
            hash.Add(member.IsFinal);
            member.AccumulateHashAndReferenceState(member._supertypes, ref hash, ref missing, ref valid);
            member.AccumulateHashAndReferenceState(member.ParameterTypes, ref hash, ref missing, ref valid);
            member.AccumulateHashAndReferenceState(member.ResultTypes, ref hash, ref missing, ref valid);
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
            valid &= member.SupertypeError == FunctionSupertypeError.None;
        }
        if (!valid || missing is not null)
        {
            foreach (var member in members)
            {
                member._isGroupValid = false;
            }
        }
    }

    private FunctionSupertypeError CheckSupertype()
    {
        if (_supertypes.Length > 1)
        {
            return FunctionSupertypeError.Multiple;
        }
        if (_supertypes is not [{ FunctionType: { } parent }])
        {
            return FunctionSupertypeError.None;
        }
        if (!IsSupertypeOrderValid(parent))
        {
            return FunctionSupertypeError.Forward;
        }
        if (parent.IsFinal)
        {
            return FunctionSupertypeError.Final;
        }
        return
            WasmValueType.AcceptsAll(ParameterTypes, parent.ParameterTypes)
            && WasmValueType.AcceptsAll(parent.ResultTypes, ResultTypes)
            ? FunctionSupertypeError.None
            : FunctionSupertypeError.Signature;
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
            if (value.FunctionType is { } target && IsInSameGroup(target))
            {
                // グループ内の参照は投影位置で表し、参照先を再展開しない。
                hash.Add(value.IsNullable);
                hash.Add(target._position);
            }
            else
            {
                hash.Add(value);
                missing ??= value.Unresolved;
                valid &= value.FunctionType?._isGroupValid != false;
            }
        }
    }

    private bool IsInSameGroup(WasmFunctionType other)
    {
        return ReferenceEquals(this, other) || (_group is not null && ReferenceEquals(_group, other._group));
    }

    private bool IsSupertypeOrderValid(WasmFunctionType parent)
    {
        // グループ外の参照先は先行グループに属する。同じグループでは先行する宣言だけを上位型にできる。
        return !IsInSameGroup(parent) || parent._position < _position;
    }

    private WasmFunctionType GetGroupMember(int position)
    {
        return _group?[position] ?? this;
    }

    /// <summary>
    /// 型付き関数参照を含む引数型と結果型から関数型を構築する
    /// </summary>
    public static WasmFunctionType Create(ReadOnlySpan<WasmValueType> parameters, ReadOnlySpan<WasmValueType> results)
    {
        foreach (var valueType in parameters)
        {
            ValidateKind(valueType.Kind, nameof(parameters));
        }
        foreach (var valueType in results)
        {
            ValidateKind(valueType.Kind, nameof(results));
        }
        return new WasmFunctionType(ImmutableArray.Create(parameters), ImmutableArray.Create(results));
    }

    /// <summary>
    /// 再帰グループ内の型番号を、そのグループの関数型への参照に置き換える
    /// </summary>
    internal static WasmFunctionType FromDecodedGroup(ReadOnlySpan<FunctionTypeDeclaration> declarations, int firstIndex)
    {
        if (declarations.Length == 1)
        {
            var type = new WasmFunctionType(null, 0, declarations[0].IsFinal);
            type.SetDecodedDeclaration(declarations[0], firstIndex, [type]);
            CompleteGroup([type]);
            return type;
        }

        // すべての参照先を確保してから、個々の型の引数と結果を確定する。
        var members = new WasmFunctionType[declarations.Length];
        for (var index = 0; index < members.Length; index++)
        {
            members[index] = new WasmFunctionType(members, index, declarations[index].IsFinal);
        }
        for (var index = 0; index < members.Length; index++)
        {
            members[index].SetDecodedDeclaration(declarations[index], firstIndex, members);
        }
        CompleteGroup(members);
        return members[0];
    }

    internal void AppendGroupTo(List<WasmFunctionType> types)
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

    private void SetDecodedDeclaration(
        FunctionTypeDeclaration declaration,
        int firstIndex,
        ReadOnlySpan<WasmFunctionType> members
    )
    {
        _supertypes = ResolveReferences(declaration.Supertypes, firstIndex, members);
        SetSignature(
            ResolveReferences(declaration.Parameters, firstIndex, members),
            ResolveReferences(declaration.Results, firstIndex, members)
        );
    }

    private static ImmutableArray<WasmValueType> ResolveReferences(
        ImmutableArray<WasmValueType> values,
        int firstIndex,
        ReadOnlySpan<WasmFunctionType> members
    )
    {
        ImmutableArray<WasmValueType>.Builder? resolved = null;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (value.FunctionType is null && value.Unresolved is { } reference)
            {
                var position = reference.Index - (uint)firstIndex;
                if (position < (uint)members.Length)
                {
                    resolved ??= values.ToBuilder();
                    resolved[index] = WasmValueType.FunctionReference(members[(int)position], value.IsNullable);
                }
            }
        }
        return resolved?.MoveToImmutable() ?? values;
    }

    /// <summary>
    /// この関数型を持つ関数を指すref.func命令の結果型を、指定した仕様版の規則で返す
    /// </summary>
    internal WasmValueType GetRefFuncResultType(WasmSpecVersion specVersion)
    {
        return specVersion == WasmSpecVersion.Core3 ? WasmValueType.FunctionReference(this, false) : WasmValueKind.FuncRef;
    }

    /// <summary>
    /// モジュールの型番号に依存せず、参照先を含めた関数型の同値性を判定する
    /// </summary>
    public bool IsEquivalentTo(WasmFunctionType other)
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
    /// 指定した関数型と同値か、宣言した上位型を通じてその型に代入できるかを返す
    /// </summary>
    internal bool IsSubtypeOf(WasmFunctionType expected)
    {
        var current = this;
        while (!current.IsEquivalentTo(expected))
        {
            if (
                !current._isGroupValid
                || current._supertypes is not [{ FunctionType: { } supertype }]
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
    internal bool HasSameStructure(WasmFunctionType other)
    {
        if (_position != other._position)
        {
            return false;
        }
        (WasmFunctionType Left, WasmFunctionType Right)? firstPending = (this, other);
        Stack<(WasmFunctionType Left, WasmFunctionType Right)>? additionalPending = null;
        HashSet<(WasmFunctionType Left, WasmFunctionType Right)>? visited = null;
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
                if (
                    a.IsFinal != b.IsFinal
                    || !CompareValues(a, b, a._supertypes, b._supertypes)
                    || !CompareValues(a, b, a.ParameterTypes, b.ParameterTypes)
                    || !CompareValues(a, b, a.ResultTypes, b.ResultTypes)
                )
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

        bool CompareValues(
            WasmFunctionType leftOwner,
            WasmFunctionType rightOwner,
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
                var a = first[index];
                var b = second[index];
                if (!a.HasSameForm(b))
                {
                    return false;
                }
                if (a.FunctionType is { } nested)
                {
                    var otherTarget = b.FunctionType!;
                    var local = leftOwner.IsInSameGroup(nested);
                    if (nested._position != otherTarget._position || local != rightOwner.IsInSameGroup(otherTarget))
                    {
                        return false;
                    }
                    if (local || ReferenceEquals(nested, otherTarget))
                    {
                        continue;
                    }
                    // 投影位置を確認した後は、外側のグループの定義全体を一度だけ比較する。
                    var pair = (nested.GetGroupMember(0), otherTarget.GetGroupMember(0));
                    if ((firstPending is { } pending && pending == pair) || (visited is not null && !visited.Add(pair)))
                    {
                        continue;
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
                            additionalPending = new Stack<(WasmFunctionType, WasmFunctionType)>();
                            visited = [firstPair, pair];
                        }
                        additionalPending.Push(pair);
                    }
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 関数型に含まれる値型がCore 2.0で定義されていることを確認する
    /// </summary>
    /// <param name="kinds">検査する値型の並び</param>
    /// <param name="paramName">不正な値型がある場合の例外に設定する引数名</param>
    /// <exception cref="ArgumentOutOfRangeException">Core 2.0で定義されていない値型を含む場合</exception>
    private static ImmutableArray<WasmValueType> ConvertKinds(ReadOnlySpan<WasmValueKind> kinds, string paramName)
    {
        var types = ImmutableArray.CreateBuilder<WasmValueType>(kinds.Length);
        foreach (var kind in kinds)
        {
            ValidateKind(kind, paramName);
            types.Add(kind);
        }
        return types.MoveToImmutable();
    }

    private static void ValidateKind(WasmValueKind kind, string paramName)
    {
        if (
            kind
            is not (
                WasmValueKind.I32
                or WasmValueKind.I64
                or WasmValueKind.F32
                or WasmValueKind.F64
                or WasmValueKind.V128
                or WasmValueKind.FuncRef
                or WasmValueKind.ExternRef
            )
        )
        {
            throw new ArgumentOutOfRangeException(paramName, kind, "Core 2.0で定義されていないvalueの種類です。");
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var remaining = MaximumDisplayedValueTypes;
        return Describe(ref remaining, []);
    }

    /// <summary>
    /// リンク診断に表示する引数型と結果型を返す
    /// </summary>
    internal string DescribeLinkSignature()
    {
        // Core 2.0の平坦な型列はすべて表示し、型付き関数参照を展開する場合は引数と結果で上限を共有する。
        var remaining =
            ParameterTypes.Any(x => x.FunctionType is not null) || ResultTypes.Any(x => x.FunctionType is not null)
                ? MaximumDisplayedValueTypes
                : int.MaxValue;
        var path = ImmutableStack<WasmFunctionType>.Empty.Push(this);
        var parameters = DescribeValues(ParameterTypes, ", ", ref remaining, path);
        var results = DescribeValues(ResultTypes, ", ", ref remaining, path);
        return $"[{parameters}] -> [{results}]";
    }

    private string Describe(ref int remaining, ImmutableStack<WasmFunctionType> ancestors)
    {
        // 表示中の型に戻る参照は、その型の再帰グループ内の位置で示す。
        foreach (var ancestor in ancestors)
        {
            if (ReferenceEquals(ancestor, this))
            {
                return $"(rec {_position})";
            }
        }
        var path = ancestors.Push(this);
        var parameters = DescribeValues(ParameterTypes, " ", ref remaining, path);
        var results = DescribeValues(ResultTypes, " ", ref remaining, path);
        return $"(func (param {parameters}) (result {results}))";
    }

    private static string DescribeValues(
        ImmutableArray<WasmValueType> types,
        string separator,
        ref int remaining,
        ImmutableStack<WasmFunctionType> path
    )
    {
        var text = new StringBuilder();
        foreach (var type in types)
        {
            if (text.Length != 0)
            {
                text.Append(separator);
            }
            // 入れ子と同じ型の再参照を通じて共有する上限で、診断の長さと再帰の深さを抑える。
            if (remaining == 0)
            {
                text.Append('…');
                break;
            }
            remaining--;
            text.Append(type.Describe(type.FunctionType?.Describe(ref remaining, path)));
        }
        return text.ToString();
    }
}
