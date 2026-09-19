namespace WasmSharp.Execution;

/// <summary>
/// 呼び出し時のinstanceを受け取るcallbackを持つホスト関数
/// </summary>
/// <param name="type">instanceを含めないWasmの引数と結果の型</param>
/// <param name="callback">所有済みの結果を返す処理</param>
internal sealed class WasmInstanceHostFunction(
    WasmFunctionType type,
    WasmHostInstanceCallback callback
) : WasmFunction
{
    /// <inheritdoc />
    public override WasmFunctionType Type { get; } = type;

    /// <summary>
    /// 呼び出し時のinstanceを受け取り、所有済みの結果を返す処理
    /// </summary>
    internal WasmHostInstanceCallback Callback { get; } = callback;
}
