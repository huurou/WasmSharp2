using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class SimdHandlerFixture
{
    public static (
        ExecutionResult Result,
        WasmValue Value,
        WasmValue Sentinel,
        int ValueCount
    ) Execute(
        NumericHandlerFixture.Handler handler,
        ReadOnlySpan<WasmValue> operands,
        byte laneIndex = 0,
        ImmutableArray<byte> shuffleLanes = default
    )
    {
        var function = ExecutionFunctionFixture.Create([], maxOperandStack: operands.Length + 1);
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

            var instruction = new Instruction(
                default,
                default,
                12345678901,
                0,
                laneIndex: laneIndex,
                shuffleLanes: shuffleLanes
            );
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
