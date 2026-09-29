using WasmSharp.TestSuiteRunner.Execution;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class ModuleFixture
{
    private static readonly byte[] emptyModule_ = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];

    internal static InstantiatedModule Instantiate()
    {
        var module = WasmModule.Decode(emptyModule_).Validate();
        return new(module, module.Instantiate(new WasmImports()));
    }

    internal static ModuleCommand CreateCommand(int index, string? name)
    {
        return new(index, index + 1, name, $"a.{index}.wasm", ScriptModuleType.Binary);
    }
}
