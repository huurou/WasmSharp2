using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class ModuleFixture
{
    private static readonly byte[] __emptyModule = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];

    internal static InstantiatedModule Instantiate(WasmExecutionContext context)
    {
        var module = WasmModule.Decode(__emptyModule, WasmSpecVersion.Core2).Validate();
        return new(module, module.Instantiate(context, new WasmImports()));
    }

    internal static ModuleCommand CreateCommand(int index, string? name)
    {
        return new(index, index + 1, name, $"a.{index}.wasm", ScriptModuleType.Binary);
    }
}
