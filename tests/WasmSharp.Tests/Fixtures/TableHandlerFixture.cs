using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;

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

    public static DefinedFunction CreateFunction(
        ImmutableArray<WasmTable> tables,
        params WasmValue[][] elements
    )
    {
        var globals = ImmutableArray.CreateBuilder<WasmGlobal>();
        var definitions = new List<ElementDefinition>();
        foreach (var element in elements)
        {
            var initializers = new List<IEnumerable<DecodedInstruction>>();
            foreach (var value in element)
            {
                initializers.Add([
                    DecodedInstruction.WithIndex(new(0, 0x23), (uint)globals.Count, 0),
                    DecodedInstruction.WithoutImmediate(new(0, 0x0B), 1),
                ]);
                globals.Add(new(new(value.Kind, false), value));
            }
            definitions.Add(ElementDefinition.Passive(element[0].Kind, [.. initializers], 0));
        }
        var module = new WasmModule(
            [new([], [])],
            [new(0, 30, [], []), new(0, 12345678900, [], [])],
            [],
            12345678910,
            [],
            [],
            [],
            [],
            null,
            elementSegments: [.. definitions]
        )
        {
            FunctionCodes = [new([], [], 0), new([], [], 4)],
        };
        return (DefinedFunction)
            new WasmInstance(module, new(10), [], globals.ToImmutable(), [], tables).Functions[1];
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
        ReadOnlySpan<WasmValue> operands,
        uint secondaryIndex = 0
    )
    {
        var caller = CreateFunction([]);
        var context = InterpreterContext.Enter(WasmExecutionOptions.Default, out var isOutermost);
        try
        {
            context.EnsureCapacity(0, 0, new(WasmProcessingStage.Invoke));
            context.EnterFrame(caller, 0);
            context.EnsureCapacity(0, 4, new(WasmProcessingStage.Invoke));
            context.EnterFrame(function, 0);
            context.PushValue(NumericHandlerFixture.Sentinel);
            foreach (var operand in operands)
            {
                context.PushValue(operand);
            }

            var instruction = new Instruction(
                default,
                default,
                BYTE_OFFSET,
                tableIndex,
                secondaryIndex: secondaryIndex
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
