using System.Collections.Immutable;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// globalの型 実行前の初期化式と入力位置
/// </summary>
/// <param name="type">globalの値型と可変性</param>
/// <param name="initializer">構築時にコピーする初期化式の命令列</param>
/// <param name="byteOffset">入力バイナリ上のglobal宣言のバイト位置</param>
internal sealed class GlobalDefinition(
    WasmGlobalType type,
    ReadOnlySpan<DecodedInstruction> initializer,
    long byteOffset
)
{
    /// <summary>
    /// globalの値型と可変性
    /// </summary>
    internal WasmGlobalType Type { get; } = type;

    /// <summary>
    /// 終端を含む未評価の初期化式
    /// </summary>
    internal ImmutableArray<DecodedInstruction> Initializer { get; } =
        ImmutableArray.Create(initializer);

    /// <summary>
    /// 入力バイナリ上のglobal宣言のバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;
}
