namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供するglobalを保持する
/// </summary>
internal sealed class GlobalExternalValue(WasmGlobal value) : WasmExternalValue
{
    /// <summary>
    /// 提供するglobal
    /// </summary>
    internal WasmGlobal Value { get; } = value;
}
