namespace WasmSharp.Modules.ExternalValues;

/// <summary>
/// Wasmモジュールに提供するmemoryを保持する
/// </summary>
/// <param name="value">提供登録とimport先で共有するmemoryの実体</param>
internal sealed class MemoryExternalValue(WasmMemory value) : ExternalValue
{
    /// <summary>
    /// 提供するmemory
    /// </summary>
    internal WasmMemory Value { get; } = value;
}
