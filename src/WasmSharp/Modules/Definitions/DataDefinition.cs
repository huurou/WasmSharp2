using System.Collections.Immutable;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// データセグメントの初期バイト列、任意の初期化先と入力位置
/// </summary>
/// <param name="bytes">構築時にコピーする初期バイト列</param>
/// <param name="activeTarget">アクティブデータセグメントの初期化先 パッシブの場合はnull</param>
/// <param name="byteOffset">入力バイナリ上のデータセグメントのバイト位置</param>
internal sealed class DataDefinition(
    ReadOnlySpan<byte> bytes,
    ActiveDataTarget? activeTarget,
    long byteOffset
)
{
    /// <summary>
    /// 入力から独立した初期バイト列
    /// </summary>
    internal ImmutableArray<byte> Bytes { get; } = ImmutableArray.Create(bytes);

    /// <summary>
    /// アクティブデータセグメントの初期化先 パッシブの場合はnull
    /// </summary>
    internal ActiveDataTarget? ActiveTarget { get; } = activeTarget;

    /// <summary>
    /// 入力バイナリ上のデータセグメントのバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;
}

/// <summary>
/// アクティブデータセグメントが参照するメモリと未評価のオフセット式
/// </summary>
/// <param name="memoryIndex">初期化先の未検証のメモリインデックス</param>
/// <param name="offsetExpression">構築時にコピーするオフセット式の命令列</param>
internal sealed class ActiveDataTarget(
    uint memoryIndex,
    ReadOnlySpan<DecodedInstruction> offsetExpression
)
{
    /// <summary>
    /// 初期化先の未検証のメモリインデックス
    /// </summary>
    internal uint MemoryIndex { get; } = memoryIndex;

    /// <summary>
    /// 終端を含む未評価のオフセット式
    /// </summary>
    internal ImmutableArray<DecodedInstruction> OffsetExpression { get; } =
        ImmutableArray.Create(offsetExpression);
}

/// <summary>
/// データ数セクションで宣言した個数と入力位置
/// </summary>
/// <param name="Count">宣言されたデータセグメントの個数</param>
/// <param name="ByteOffset">入力バイナリ上の個数のバイト位置</param>
internal readonly record struct DataCountDefinition(uint Count, long ByteOffset);
