namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供するtableを保持する
/// </summary>
internal sealed class TableExternalValue(WasmTable value) : ExternalValue
{
    /// <summary>
    /// 提供するtable
    /// </summary>
    internal WasmTable Value { get; } = value;
}
