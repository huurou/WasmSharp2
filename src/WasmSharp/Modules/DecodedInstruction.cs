using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 入力命令の識別子、即値とバイト位置
/// </summary>
public readonly struct DecodedInstruction
{
    /// <summary>
    /// 通常命令またはprefix付き拡張命令の識別子
    /// </summary>
    internal OpcodeKey Opcode { get; }

    /// <summary>
    /// 命令に付随する即値
    /// </summary>
    internal WasmValue Immediate { get; }

    /// <summary>
    /// 入力バイナリ上の命令のバイト位置
    /// </summary>
    internal long ByteOffset { get; }

    /// <summary>
    /// デコードした命令の識別子、即値と入力上の位置を保持する
    /// </summary>
    /// <param name="opcode">命令の識別子</param>
    /// <param name="immediate">命令の即値</param>
    /// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
    internal DecodedInstruction(OpcodeKey opcode, WasmValue immediate, long byteOffset)
    {
        Opcode = opcode;
        Immediate = immediate;
        ByteOffset = byteOffset;
    }
}
