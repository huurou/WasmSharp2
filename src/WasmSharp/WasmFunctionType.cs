using System.Collections.Immutable;
using System.Text;
using WasmSharp.Modules.Definitions;

namespace WasmSharp;

/// <summary>
/// functionの型を表現するクラス
/// </summary>
public sealed class WasmFunctionType : WasmDefinedType
{
    internal override WasmValueKind ReferenceKind => WasmValueKind.FuncRef;

    /// <summary>
    /// null可否と参照先の型を含む引数型
    /// </summary>
    public ImmutableArray<WasmValueType> ParameterTypes { get; private set; }

    /// <summary>
    /// null可否と参照先の型を含む結果型
    /// </summary>
    public ImmutableArray<WasmValueType> ResultTypes { get; private set; }

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
    /// <exception cref="ArgumentOutOfRangeException">未対応の値型を含む場合</exception>
    public WasmFunctionType(ReadOnlySpan<WasmValueKind> parameters, ReadOnlySpan<WasmValueKind> results)
        : this(ConvertKinds(parameters, nameof(parameters)), ConvertKinds(results, nameof(results))) { }

    private WasmFunctionType(ImmutableArray<WasmValueType> parameters, ImmutableArray<WasmValueType> results)
        : base(null, 0, true)
    {
        SetSignature(parameters, results);
        CompleteGroup([this]);
    }

    internal WasmFunctionType(WasmDefinedType[]? group, int position, bool isFinal)
        : base(group, position, isFinal) { }

    private void SetSignature(ImmutableArray<WasmValueType> parameters, ImmutableArray<WasmValueType> results)
    {
        ParameterTypes = parameters;
        ResultTypes = results;
        Parameters = ImmutableArray.CreateRange(parameters, static x => x.Kind);
        Results = ImmutableArray.CreateRange(results, static x => x.Kind);
    }

    /// <summary>
    /// 具体的な参照型を含む引数型と結果型から関数型を構築する
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

    private protected override void SetDecodedComposite(
        TypeDeclaration declaration,
        int firstIndex,
        ReadOnlySpan<WasmDefinedType> members
    )
    {
        SetSignature(
            ResolveReferences(declaration.Parameters, firstIndex, members),
            ResolveReferences(declaration.Results, firstIndex, members)
        );
    }

    private protected override bool MatchesSupertype(WasmDefinedType parent)
    {
        return parent is WasmFunctionType function
            && WasmValueType.AcceptsAll(ParameterTypes, function.ParameterTypes)
            && WasmValueType.AcceptsAll(function.ResultTypes, ResultTypes);
    }

    /// <summary>
    /// 参照先を含めて関数型の同値性を判定する
    /// </summary>
    public bool IsEquivalentTo(WasmFunctionType other)
    {
        return base.IsEquivalentTo(other);
    }

    /// <summary>
    /// この関数型を持つ関数を指すref.func命令の結果型を、指定した仕様版の規則で返す
    /// </summary>
    internal WasmValueType GetRefFuncResultType(WasmSpecVersion specVersion)
    {
        return specVersion == WasmSpecVersion.Core3 ? WasmValueType.FunctionReference(this, false) : WasmValueKind.FuncRef;
    }

    /// <summary>
    /// 関数型に含まれる値の種類を確認し、宣言型に変換する
    /// </summary>
    /// <param name="kinds">検査する値型の並び</param>
    /// <param name="paramName">不正な値型がある場合の例外に設定する引数名</param>
    /// <exception cref="ArgumentOutOfRangeException">未対応の値型を含む場合</exception>
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
        if (!kind.IsSupported())
        {
            throw new ArgumentOutOfRangeException(paramName, kind, "未対応のvalueの種類です。");
        }
    }

    /// <summary>
    /// リンク診断に表示する引数型と結果型を返す
    /// </summary>
    internal string DescribeLinkSignature()
    {
        // Core 2.0の平坦な型列はすべて表示し、型付き関数参照を展開する場合は引数と結果で上限を共有する。
        var remaining =
            ParameterTypes.Any(x => x.DefinedType is not null) || ResultTypes.Any(x => x.DefinedType is not null)
                ? MaximumDisplayedValueTypes
                : int.MaxValue;
        var path = ImmutableStack<WasmDefinedType>.Empty.Push(this);
        var parameters = DescribeValues(ParameterTypes, ", ", ref remaining, path);
        var results = DescribeValues(ResultTypes, ", ", ref remaining, path);
        return $"[{parameters}] -> [{results}]";
    }

    private protected override string DescribeComposite(ref int remaining, ImmutableStack<WasmDefinedType> path)
    {
        var parameters = DescribeValues(ParameterTypes, " ", ref remaining, path);
        var results = DescribeValues(ResultTypes, " ", ref remaining, path);
        return $"(func (param {parameters}) (result {results}))";
    }

    private static string DescribeValues(
        ImmutableArray<WasmValueType> types,
        string separator,
        ref int remaining,
        ImmutableStack<WasmDefinedType> path
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
            text.Append(DescribeValue(type, ref remaining, path));
        }
        return text.ToString();
    }
}
