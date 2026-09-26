using System.Collections.Immutable;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Fixtures;

internal static class ExecutionFunctionFixture
{
    public static DefinedFunction Create(
        ImmutableArray<Instruction> instructions,
        WasmValueKind resultKind = WasmValueKind.I32,
        int maxOperandStack = 1
    )
    {
        return Create(instructions, new([], [resultKind]), [], maxOperandStack);
    }

    public static DefinedFunction Create(
        ImmutableArray<Instruction> instructions,
        WasmFunctionType type,
        ImmutableArray<LocalDeclaration> locals,
        int maxOperandStack
    )
    {
        var module = new WasmModule(
            [type],
            [new(0, 30, [], []), new(0, 12345678900, locals.AsSpan(), [])],
            [],
            12345678910,
            [],
            [],
            [],
            [],
            null
        )
        {
            FunctionCodes = [new([], [], 0), new(instructions, locals.AsSpan(), maxOperandStack)],
        };
        return (DefinedFunction)new WasmInstance(module, new(1)).Functions[1];
    }
}
