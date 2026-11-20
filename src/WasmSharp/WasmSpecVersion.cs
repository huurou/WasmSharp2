namespace WasmSharp;

/// <summary>
/// moduleのデコードと検証で前提とするWebAssembly Core仕様の版
/// </summary>
public enum WasmSpecVersion
{
    /// <summary>
    /// WebAssembly Core 2.0
    /// </summary>
    Core2,

    /// <summary>
    /// WebAssembly Core 3.0
    /// </summary>
    Core3,
}
