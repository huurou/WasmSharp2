using System.Collections.Immutable;

namespace WasmSharp.Modules;

/// <summary>
/// デコードした関数の型index、圧縮localsと入力命令
/// </summary>
public sealed class DecodedFunction
{
    /// <summary>
    /// moduleの型index空間における関数型の位置
    /// </summary>
    internal uint TypeIndex { get; }

    /// <summary>
    /// 入力バイナリ上の関数本体の開始バイト位置
    /// </summary>
    internal long BodyOffset { get; }

    /// <summary>
    /// 個数と型で圧縮したlocal宣言を保持する不変配列
    /// </summary>
    internal ImmutableArray<LocalDeclaration> Locals { get; }

    /// <summary>
    /// 入力順にデコードした命令を保持する不変配列
    /// </summary>
    internal ImmutableArray<DecodedInstruction> Instructions { get; }

    /// <summary>
    /// local宣言と命令をコピーして関数定義を構築する
    /// </summary>
    /// <param name="typeIndex">関数型のindex</param>
    /// <param name="bodyOffset">入力バイナリ上の関数本体の開始バイト位置</param>
    /// <param name="locals">構築時にコピーする圧縮local宣言</param>
    /// <param name="instructions">構築時にコピーする入力命令</param>
    internal DecodedFunction(
        uint typeIndex,
        long bodyOffset,
        ReadOnlySpan<LocalDeclaration> locals,
        ReadOnlySpan<DecodedInstruction> instructions
    )
    {
        TypeIndex = typeIndex;
        BodyOffset = bodyOffset;
        Locals = ImmutableArray.Create(locals);
        Instructions = ImmutableArray.Create(instructions);
    }
}
