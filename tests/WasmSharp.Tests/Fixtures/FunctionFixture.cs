using WasmSharp.Execution;

namespace WasmSharp.Tests.Fixtures;

internal static class FunctionFixture
{
    public static DefinedFunction Create()
    {
        var module = new WasmModule(
            [new([], [WasmValueKind.I32])],
            [new(0, 30, [], [])],
            [],
            36,
            [],
            [],
            [],
            [],
            null
        );
        return (DefinedFunction)new WasmInstance(module, WasmExecutionOptions.Default).Functions[0];
    }
}
