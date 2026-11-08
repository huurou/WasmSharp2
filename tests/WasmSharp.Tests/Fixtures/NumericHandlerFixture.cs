using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class NumericHandlerFixture
{
    public static WasmValue Sentinel { get; } = WasmValue.FromF64Bits(0xFFF8123456789ABC);

    public delegate ExecutionResult Handler(InterpreterContext context, in Instruction instruction);

    public static (
        ExecutionResult Result,
        WasmValue Value,
        WasmValue Sentinel,
        int ValueCount
    ) Execute(Handler handler, ReadOnlySpan<WasmValue> operands)
    {
        var executionContext = new WasmExecutionContext();
        var function = ExecutionFunctionFixture.Create([], maxOperandStack: operands.Length + 1);
        var context = executionContext.Enter(WasmExecutionOptions.Default, out var isOutermost);
        try
        {
            context.Stage = WasmProcessingStage.Invoke;
            context.EnsureCapacity(0, function.Code.MaxOperandStack, function);
            context.PushFrame(new(function, 0, 0));
            context.PushValue(Sentinel);
            foreach (var operand in operands)
            {
                context.PushValue(operand);
            }

            var instruction = new Instruction(default, default, 12345678901, 0);
            var result = handler(context, in instruction);
            return (result, context.PeekValue(), context.GetValue(0), context.ValueCount);
        }
        finally
        {
            executionContext.Exit(isOutermost);
            context.Restore(0, 0, 0);
        }
    }
}
