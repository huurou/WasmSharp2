namespace WasmSharp.Modules.Definitions;

/// <summary>
/// memoryの未検証の型と入力位置
/// </summary>
/// <param name="ByteOffset">入力バイナリ上のmemory型記述のバイト位置</param>
/// <param name="Type">アドレス型と未検証のページ数の上下限</param>
internal sealed record MemoryDefinition(long ByteOffset, WasmMemoryType Type)
{
    internal MemoryDefinition(WasmLimits limits, long byteOffset)
        : this(byteOffset, new WasmMemoryType(limits)) { }
}
