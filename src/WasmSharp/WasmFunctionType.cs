using System.Collections.Immutable;
using System.Text;

namespace WasmSharp;

/// <summary>
/// functionの型を表現するクラス
/// </summary>
public sealed class WasmFunctionType
{
    private const int MaximumDisplayedValueTypes = 16;

    /// <summary>
    /// null可否と参照先の関数型を含む引数型
    /// </summary>
    public ImmutableArray<WasmValueType> ParameterTypes { get; }

    /// <summary>
    /// null可否と参照先の関数型を含む結果型
    /// </summary>
    public ImmutableArray<WasmValueType> ResultTypes { get; }

    internal WasmValueType.UnresolvedReference? Unresolved { get; }

    internal int StructuralHashCode { get; }

    /// <summary>
    /// 引数の型を保持する不変配列
    /// </summary>
    public ImmutableArray<WasmValueKind> Parameters { get; }

    /// <summary>
    /// 戻り値の型を保持する不変配列
    /// </summary>
    public ImmutableArray<WasmValueKind> Results { get; }

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
        ParameterTypes = parameters;
        ResultTypes = results;
        Parameters = [.. parameters.Select(x => x.Kind)];
        Results = [.. results.Select(x => x.Kind)];

        var hash = new HashCode();
        hash.Add(parameters.Length);
        foreach (var type in parameters)
        {
            Unresolved ??= type.Unresolved;
            hash.Add(type);
        }
        foreach (var type in results)
        {
            Unresolved ??= type.Unresolved;
            hash.Add(type);
        }
        StructuralHashCode = hash.ToHashCode();
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
    /// モジュールの型番号に依存せず、参照先を含めた関数型の同値性を判定する
    /// </summary>
    public bool IsEquivalentTo(WasmFunctionType other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Stack<(WasmFunctionType Left, WasmFunctionType Right)>? pending = null;
        HashSet<(WasmFunctionType Left, WasmFunctionType Right)>? visited = null;
        var left = this;
        var right = other;
        while (true)
        {
            if (
                !ReferenceEquals(left, right)
                && (
                    left.StructuralHashCode != right.StructuralHashCode
                    || !CompareValues(left.ParameterTypes, right.ParameterTypes)
                    || !CompareValues(left.ResultTypes, right.ResultTypes)
                )
            )
            {
                return false;
            }
            if (pending is null || !pending.TryPop(out var next))
            {
                return true;
            }
            (left, right) = next;
        }

        bool CompareValues(ImmutableArray<WasmValueType> first, ImmutableArray<WasmValueType> second)
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
                // 同じ型を繰り返し参照する宣言でも、一組の関数型を一度だけ比較する。
                if (a.FunctionType is { } nested && !ReferenceEquals(nested, b.FunctionType))
                {
                    var pair = (nested, b.FunctionType!);
                    visited ??= [];
                    if (visited.Add(pair))
                    {
                        pending ??= new Stack<(WasmFunctionType, WasmFunctionType)>();
                        pending.Push(pair);
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
        return Describe(ref remaining);
    }

    private string Describe(ref int remaining)
    {
        return $"(func (param {DescribeValues(ParameterTypes, ref remaining)}) (result {DescribeValues(ResultTypes, ref remaining)}))";
    }

    private static string DescribeValues(ImmutableArray<WasmValueType> types, ref int remaining)
    {
        var text = new StringBuilder();
        foreach (var type in types)
        {
            if (text.Length != 0)
            {
                text.Append(' ');
            }
            // 入れ子と同じ型の再参照を通じて共有する上限で、診断の長さと再帰の深さを抑える。
            if (remaining == 0)
            {
                text.Append('…');
                break;
            }
            remaining--;
            text.Append(type.Describe(type.FunctionType?.Describe(ref remaining)));
        }
        return text.ToString();
    }
}
