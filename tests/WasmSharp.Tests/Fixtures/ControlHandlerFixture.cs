using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class ControlHandlerFixture
{
    public static WasmValue Local { get; } = WasmValue.FromI64(73);
    public static WasmValue Sentinel { get; } = WasmValue.FromF64Bits(0xFFF8123456789ABC);

    public delegate ExecutionResult Handler(InterpreterContext context, in Instruction instruction);

    public static (
        ExecutionResult Result,
        ImmutableArray<WasmValue> Values,
        ExecutionFrame Before,
        ExecutionFrame After,
        int FrameCount,
        int CallDepth,
        WasmProcessingStage Stage
    ) Execute(
        Handler handler,
        in Instruction instruction,
        ReadOnlySpan<WasmValue> operands,
        ImmutableArray<BranchTarget> targets = default
    )
    {
        var executionContext = new WasmExecutionContext();
        var function = ExecutionFunctionFixture.Create(
            [default, default, default, default],
            new([WasmValueKind.I64], []),
            [],
            operands.Length + 1,
            targets
        );
        var frame = new ExecutionFrame(function, 0, 1) { Pc = 2 };
        var context = executionContext.Enter(new(10), out var isOutermost);
        try
        {
            context.Stage = WasmProcessingStage.Invoke;
            context.EnsureCapacity(1, function.Code.MaxOperandStack, function);
            context.TryEnterCall();
            context.PushFrame(frame);
            context.PushValue(Local);
            context.PushValue(Sentinel);
            foreach (var operand in operands)
            {
                context.PushValue(operand);
            }

            var result = handler(context, in instruction);
            return (
                result,
                context.CopyValues(0, context.ValueCount),
                frame,
                context.GetFrame(0),
                context.FrameCount,
                context.CallDepth,
                context.Stage
            );
        }
        finally
        {
            executionContext.Exit(isOutermost);
            context.Restore(0, 0, 0);
        }
    }
}
