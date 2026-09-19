namespace WasmSharp.Modules.Definitions;

/// <summary>
/// memoryの未検証のlimitsと入力位置
/// </summary>
/// <param name="Limits">未検証のページ数の最小値と任意の最大値</param>
/// <param name="ByteOffset">入力バイナリ上のmemory型記述のバイト位置</param>
internal sealed record MemoryDefinition(WasmLimits Limits, long ByteOffset);
