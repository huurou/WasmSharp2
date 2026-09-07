using WasmSharp.Instructions;

namespace WasmSharp.Execution;

/// <summary>
/// 実行可能な線形命令と即値、入力上の位置
/// </summary>
/// <remarks>検証と線形化で確定した命令を保持する</remarks>
/// <param name="opcode">命令宣言から生成された実行opcode</param>
/// <param name="immediate">命令の即値</param>
/// <param name="byteOffset">入力バイナリ上の命令のバイト位置</param>
internal readonly struct Instruction(ExecutionOpcode opcode, WasmValue immediate, long byteOffset)
{
    /// <summary>
    /// 命令宣言から生成された実行opcode
    /// </summary>
    internal ExecutionOpcode Opcode { get; } = opcode;

    /// <summary>
    /// 命令の即値。関数終端では参照しない
    /// </summary>
    internal WasmValue Immediate { get; } = immediate;

    /// <summary>
    /// 入力バイナリ上の命令のバイト位置
    /// </summary>
    internal long ByteOffset { get; } = byteOffset;
}
