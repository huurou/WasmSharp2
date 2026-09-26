namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供する関数を保持する
/// </summary>
internal sealed class FunctionExternalValue(WasmFunction value) : ExternalValue
{
    /// <summary>
    /// 提供する関数
    /// </summary>
    internal WasmFunction Value { get; } = value;
}
