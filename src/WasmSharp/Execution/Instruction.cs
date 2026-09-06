using WasmSharp.Instructions;

namespace WasmSharp.Execution;

/// <summary>
/// 実行可能な線形命令と即値、入力上の位置
/// </summary>
public readonly struct Instruction
{
    /// <summary>
    /// 命令宣言から生成された実行opcode
    /// </summary>
    internal ExecutionOpcode Opcode { get; }

    /// <summary>
    /// 命令の即値。関数終端では参照しない
    /// </summary>
    internal WasmValue Immediate { get; }

    /// <summary>
    /// 入力バイナリ上の命令のバイト位置
    /// </summary>
    internal long ByteOffset { get; }

    /// <summary>
    /// 検証と線形化で確定した命令を保持する
    /// </summary>
    internal Instruction(ExecutionOpcode opcode, WasmValue immediate, long byteOffset)
    {
        Opcode = opcode;
        Immediate = immediate;
        ByteOffset = byteOffset;
    }
}
