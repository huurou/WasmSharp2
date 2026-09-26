namespace WasmSharp.Execution;

/// <summary>
/// instanceを受け取らないcallbackを持つホスト関数
/// </summary>
/// <param name="type">Wasmの引数と結果の型</param>
/// <param name="callback">所有済みの結果を返す処理</param>
internal sealed class HostFunction(WasmFunctionType type, WasmHostCallback callback) : WasmFunction
{
    /// <inheritdoc />
    public override WasmFunctionType Type { get; } = type;

    /// <summary>
    /// instanceを受け取らず、所有済みの結果を返す処理
    /// </summary>
    internal WasmHostCallback Callback { get; } = callback;
}
