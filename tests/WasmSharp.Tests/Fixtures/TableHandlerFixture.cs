using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class TableHandlerFixture
{
    public const long BYTE_OFFSET = 12345678901;

    public static WasmValue CreateReference(WasmValueKind kind, bool isNull = false)
    {
        return kind == WasmValueKind.FuncRef
            ? WasmValue.FromFuncRef(
                isNull ? null : WasmFunction.CreateHost(new([], []), _ => new([]))
            )
            : WasmValue.FromExternRef(isNull ? null : new object());
    }

    public static DefinedFunction CreateFunction(ImmutableArray<WasmTable> tables)
    {
        var module = new WasmModule(
            [new([], [])],
            [new(0, 30, [], []), new(0, 12345678900, [], [])],
            [],
            12345678910,
            [],
            [],
            [],
            [],
            null
        )
        {
            FunctionCodes = [new([], [], 0), new([], [], 3)],
        };
        return (DefinedFunction)new WasmInstance(module, new(10), [], [], [], tables).Functions[1];
    }

    public static (
        ExecutionResult Result,
        WasmValue Value,
        WasmValue Sentinel,
        int ValueCount
    ) Execute(
        NumericHandlerFixture.Handler handler,
        DefinedFunction function,
        uint tableIndex,
        ReadOnlySpan<WasmValue> operands
    )
    {
        var caller = CreateFunction([]);
        var context = InterpreterContext.Enter(WasmExecutionOptions.Default, out var isOutermost);
        try
        {
            context.EnsureCapacity(0, 0, new(WasmProcessingStage.Invoke));
            context.EnterFrame(caller, 0);
            context.EnsureCapacity(0, 3, new(WasmProcessingStage.Invoke));
            context.EnterFrame(function, 0);
            context.PushValue(NumericHandlerFixture.Sentinel);
            foreach (var operand in operands)
            {
                context.PushValue(operand);
            }

            var instruction = new Instruction(default, default, BYTE_OFFSET, tableIndex);
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
