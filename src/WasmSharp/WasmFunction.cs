namespace WasmSharp;

/// <summary>
/// functionを表現するクラス
/// </summary>
public sealed class WasmFunction
{
    /// <summary>
    /// functionの型
    /// </summary>
    public WasmFunctionType Type => throw new NotImplementedException();

    /// <summary>
    /// 指定した引数で関数を呼び出す
    /// </summary>
    /// <param name="arguments">関数に渡すvalueのコレクション</param>
    /// <param name="options">実行時のオプション設定</param>
    /// <returns>関数が返すvalueのコレクション</returns>
    public WasmResults Invoke(
        ReadOnlySpan<WasmValue> arguments,
        WasmExecutionOptions? options = default
    )
    {
        throw new NotImplementedException();
    }
}
