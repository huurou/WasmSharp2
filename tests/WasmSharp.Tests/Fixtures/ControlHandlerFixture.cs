using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class ControlHandlerFixture
{
    private static readonly WasmValue local_ = WasmValue.FromI64(73);
    private static readonly WasmValue sentinel_ = WasmValue.FromF64Bits(0xFFF8123456789ABC);

    public static WasmValue Local => local_;
    public static WasmValue Sentinel => sentinel_;

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
        var function = ExecutionFunctionFixture.Create(
            [default, default, default, default],
            new([WasmValueKind.I64], []),
            [],
            operands.Length + 1,
            targets
        );
        var frame = new ExecutionFrame(function, 0, 1) { Pc = 2 };
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        try
        {
            context.Stage = WasmProcessingStage.Invoke;
            context.EnsureCapacity(
                1,
                function.Code.MaxOperandStack,
                new(WasmProcessingStage.Invoke)
            );
            context.TryEnterCall();
            context.PushFrame(frame);
            context.PushValue(local_);
            context.PushValue(sentinel_);
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
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }
    }
}
