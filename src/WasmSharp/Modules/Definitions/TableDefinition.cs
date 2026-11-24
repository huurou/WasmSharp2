using System.Collections.Immutable;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// テーブルの未検証の型と入力位置
/// </summary>
/// <param name="ByteOffset">入力バイナリ上のテーブル型記述のバイト位置</param>
/// <param name="Type">要素型とアドレス型、未検証の要素数の上下限</param>
internal sealed record TableDefinition(long ByteOffset, WasmTableType Type)
{
    /// <summary>
    /// 明示した初期化式の命令列 省略した場合は空配列
    /// </summary>
    internal ImmutableArray<DecodedInstruction> Initializer { get; init; } = [];

    internal TableDefinition(WasmValueKind elementType, WasmLimits limits, long byteOffset)
        : this(byteOffset, new WasmTableType(elementType, limits)) { }
}
