using System.Collections.Immutable;

namespace WasmSharp;

/// <summary>
/// functionの型を表現するクラス
/// </summary>
public sealed class WasmFunctionType
{
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
    public WasmFunctionType(
        ReadOnlySpan<WasmValueKind> parameters,
        ReadOnlySpan<WasmValueKind> results
    )
    {
        ValidateKinds(parameters, nameof(parameters));
        ValidateKinds(results, nameof(results));
        Parameters = ImmutableArray.Create(parameters);
        Results = ImmutableArray.Create(results);
    }

    private static void ValidateKinds(ReadOnlySpan<WasmValueKind> kinds, string paramName)
    {
        foreach (var kind in kinds)
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
                throw new ArgumentOutOfRangeException(
                    paramName,
                    kind,
                    "Core 2.0で定義されていないvalueの種類です。"
                );
            }
        }
    }
}
