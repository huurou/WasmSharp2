using System.Collections.Immutable;

namespace WasmSharp.Modules;

/// <summary>
/// デコードした関数の型index、圧縮localsと入力命令
/// </summary>
/// <remarks>local宣言と命令をコピーして関数定義を構築する</remarks>
/// <param name="typeIndex">関数型のindex</param>
/// <param name="bodyOffset">入力バイナリ上の関数本体の開始バイト位置</param>
/// <param name="locals">構築時にコピーする圧縮local宣言</param>
/// <param name="instructions">構築時にコピーする入力命令</param>
internal sealed class DecodedFunction(
    uint typeIndex,
    long bodyOffset,
    ReadOnlySpan<LocalDeclaration> locals,
    ReadOnlySpan<DecodedInstruction> instructions
)
{
    /// <summary>
    /// moduleの型index空間における関数型の位置
    /// </summary>
    internal uint TypeIndex { get; } = typeIndex;

    /// <summary>
    /// 入力バイナリ上の関数本体の開始バイト位置
    /// </summary>
    internal long BodyOffset { get; } = bodyOffset;

    /// <summary>
    /// 個数と型で圧縮したlocal宣言を保持する不変配列
    /// </summary>
    internal ImmutableArray<LocalDeclaration> Locals { get; } = ImmutableArray.Create(locals);

    /// <summary>
    /// 入力順にデコードした命令を保持する不変配列
    /// </summary>
    internal ImmutableArray<DecodedInstruction> Instructions { get; } =
        ImmutableArray.Create(instructions);
}
