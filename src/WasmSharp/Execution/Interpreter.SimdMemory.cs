using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 所属instanceのメモリから16バイトを読み、v128の値をoperandへ積む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレスを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのv128.load命令</param>
    /// <returns>実行の成功または範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Load(InterpreterContext context, in Instruction instruction)
    {
        var address =
            (ulong)unchecked((uint)context.PopValue().AsI32()) + instruction.MemoryArgument.Offset;
        Span<byte> bytes = stackalloc byte[16];
        if (!context.CurrentFunction.Instance.Memories[0].TryRead(address, bytes))
        {
            return ExecutionResult.Trap(
                WasmTrapReason.MemoryOutOfBounds,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
        }

        context.PushValue(SimdValue.FromLittleEndian(bytes).ToValue());
        return default;
    }

    /// <summary>
    /// operandのv128の値を所属instanceのメモリへ16バイトで書き込む
    /// </summary>
    /// <param name="context">現在の関数と検証済みのアドレス・値を持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのv128.store命令</param>
    /// <returns>実行の成功またはメモリを変更しない範囲外アクセスのtrap</returns>
    internal static ExecutionResult V128Store(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var value = SimdValue.FromValue(context.PopValue());
        var address =
            (ulong)unchecked((uint)context.PopValue().AsI32()) + instruction.MemoryArgument.Offset;
        Span<byte> bytes = stackalloc byte[16];
        value.WriteLittleEndian(bytes);
        if (!context.CurrentFunction.Instance.Memories[0].TryWrite(address, bytes))
        {
            return ExecutionResult.Trap(
                WasmTrapReason.MemoryOutOfBounds,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
        }

        return default;
    }
}
