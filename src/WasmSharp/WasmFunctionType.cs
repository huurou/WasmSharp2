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
    /// <param name="parameters">宣言順に並べた引数の型</param>
    /// <param name="results">宣言順に並べた戻り値の型</param>
    /// <exception cref="ArgumentOutOfRangeException">Core 2.0で定義されていない値型を含む場合</exception>
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

    /// <summary>
    /// 関数型に含まれる値型がCore 2.0で定義されていることを確認する
    /// </summary>
    /// <param name="kinds">検査する値型の並び</param>
    /// <param name="paramName">不正な値型がある場合の例外に設定する引数名</param>
    /// <exception cref="ArgumentOutOfRangeException">Core 2.0で定義されていない値型を含む場合</exception>
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
