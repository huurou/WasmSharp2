namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供するtableを保持する
/// </summary>
/// <param name="value">提供登録とimport先で共有するtableの実体</param>
internal sealed class TableExternalValue(WasmTable value) : ExternalValue
{
    /// <summary>
    /// 提供するtable
    /// </summary>
    internal WasmTable Value { get; } = value;
}
