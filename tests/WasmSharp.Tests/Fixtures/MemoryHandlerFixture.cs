using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Tests.Fixtures;

internal static class MemoryHandlerFixture
{
    public static Instruction CreateInstruction(
        WasmValueKind valueType,
        byte byteWidth,
        bool signExtend = false,
        uint offset = 0,
        uint alignment = 0
    )
    {
        return new(
            default,
            default,
            12345678901,
            0,
            memoryArgument: new(alignment, offset),
            memoryAccessSignature: new(valueType, byteWidth, signExtend)
        );
    }

    public static (
        ExecutionResult Result,
        WasmValue Value,
        WasmValue Sentinel,
        int ValueCount
    ) Execute(
        NumericHandlerFixture.Handler handler,
        WasmMemory memory,
        in Instruction instruction,
        ReadOnlySpan<WasmValue> operands
    )
    {
        var function = ExecutionFunctionFixture.Create(
            [],
            new([], []),
            [],
            operands.Length + 1,
            memories: [memory]
        );
        var context = InterpreterContext.Enter(WasmExecutionOptions.Default, out var isOutermost);
        try
        {
            context.EnsureCapacity(
                0,
                function.Code.MaxOperandStack,
                new(WasmProcessingStage.Invoke)
            );
            context.PushFrame(new(function, 0, 0));
            context.PushValue(NumericHandlerFixture.Sentinel);
            foreach (var operand in operands)
            {
                context.PushValue(operand);
            }

            var result = handler(context, in instruction);
            return (result, context.PeekValue(), context.GetValue(0), context.ValueCount);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }
    }
}
