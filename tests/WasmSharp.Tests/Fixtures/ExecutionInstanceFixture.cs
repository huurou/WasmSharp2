using System.Collections.Immutable;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Fixtures;

internal static class ExecutionInstanceFixture
{
    public static long GetBodyOffset(int definitionIndex)
    {
        return 1000L * (definitionIndex + 1);
    }

    public static WasmInstance Create(
        ImmutableArray<WasmFunctionType> types,
        ImmutableArray<(
            uint TypeIndex,
            ImmutableArray<LocalDeclaration> Locals,
            ImmutableArray<Instruction> Instructions,
            int MaxOperandStack
        )> functions,
        ImmutableArray<WasmFunction> importedFunctions,
        ImmutableArray<WasmGlobal> globals
    )
    {
        var module = new WasmModule(
            types.AsSpan(),
            [
                .. functions.Select(
                    (x, i) =>
                        new DecodedFunction(x.TypeIndex, GetBodyOffset(i), x.Locals.AsSpan(), [])
                ),
            ],
            [],
            1L << 40,
            [],
            [],
            [],
            [],
            null
        )
        {
            FunctionCodes =
            [
                .. functions.Select(x => new FunctionCode(
                    x.Instructions,
                    x.Locals.AsSpan(),
                    x.MaxOperandStack
                )),
            ],
        };
        return new WasmInstance(
            module,
            WasmExecutionOptions.Default,
            importedFunctions,
            globals,
            [],
            []
        );
    }
}
