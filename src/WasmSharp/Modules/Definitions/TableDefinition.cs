namespace WasmSharp.Modules.Definitions;

/// <summary>
/// テーブルの未検証の型と入力位置
/// </summary>
/// <param name="ByteOffset">入力バイナリ上のテーブル型記述のバイト位置</param>
/// <param name="Type">要素型とアドレス型、未検証の要素数の上下限</param>
internal sealed record TableDefinition(long ByteOffset, WasmTableType Type)
{
    internal TableDefinition(WasmValueKind elementType, WasmLimits limits, long byteOffset)
        : this(byteOffset, new WasmTableType(elementType, limits)) { }
}
