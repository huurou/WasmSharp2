using System.Collections.Immutable;
using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class ExecutionFunctionFixture
{
    public static WasmDefinedFunction Create(
        ImmutableArray<Instruction> instructions,
        WasmValueKind resultKind = WasmValueKind.I32,
        int maxOperandStack = 1
    )
    {
        var module = new WasmModule(
            [new([], [resultKind])],
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
            FunctionCodes = [new([], 0), new(instructions, maxOperandStack)],
        };
        return (WasmDefinedFunction)new WasmInstance(module, new(1)).Functions[1];
    }
}
