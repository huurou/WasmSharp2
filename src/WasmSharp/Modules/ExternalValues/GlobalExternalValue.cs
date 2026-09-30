namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供するglobalを保持する
/// </summary>
/// <param name="value">提供登録とimport先で共有するglobalの実体</param>
internal sealed class GlobalExternalValue(WasmGlobal value) : ExternalValue
{
    /// <summary>
    /// 提供するglobal
    /// </summary>
    internal WasmGlobal Value { get; } = value;
}
