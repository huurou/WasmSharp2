using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 入力命令の識別子、即値とバイト位置
/// </summary>
/// <remarks>デコードした命令の識別子、即値と入力上の位置を保持する</remarks>
/// <param name="opcode">命令の識別子</param>
/// <param name="immediate">命令の即値</param>
/// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
/// <param name="index">命令に埋め込まれた関数・ローカル変数などの番号（番号を指定しない命令では参照しない）</param>
internal readonly struct DecodedInstruction(
    OpcodeKey opcode,
    WasmValue immediate,
    long byteOffset,
    uint index = 0
)
{
    /// <summary>
    /// 通常命令またはprefix付き拡張命令の識別子
    /// </summary>
    internal OpcodeKey Opcode { get; } = opcode;

    /// <summary>
    /// 命令に付随する即値
    /// </summary>
    internal WasmValue Immediate { get; } = immediate;

    /// <summary>
    /// 命令に埋め込まれた関数・ローカル変数などの番号
    /// </summary>
    internal uint Index { get; } = index;

    /// <summary>
    /// 入力バイナリ上の命令のバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;
}
