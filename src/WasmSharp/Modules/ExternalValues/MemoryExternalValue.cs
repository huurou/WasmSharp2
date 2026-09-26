namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供するmemoryを保持する
/// </summary>
internal sealed class MemoryExternalValue(WasmMemory value) : ExternalValue
{
    /// <summary>
    /// 提供するmemory
    /// </summary>
    internal WasmMemory Value { get; } = value;
}
