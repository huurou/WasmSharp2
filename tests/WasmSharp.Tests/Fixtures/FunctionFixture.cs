namespace WasmSharp.Tests.Fixtures;

public static class FunctionFixture
{
    public static WasmFunction Create()
    {
        var module = new WasmModule([new([], [WasmValueKind.I32])], [new(0, 30, [], [])], [], 36);
        return new WasmInstance(module, WasmExecutionOptions.Default).Functions[0];
    }
}
