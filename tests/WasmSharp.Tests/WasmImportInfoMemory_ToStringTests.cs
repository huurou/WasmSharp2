using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmImportInfoMemory_ToStringTests
{
    [Test]
    [Arguments("0400")]
    [Arguments("0480808080808040")]
    [Arguments("008080808010")]
    public async Task 調査した要求型を32ビットのlimitsで表せない_メモリの型情報を表示する(string limits)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "m", 2, Convert.FromHexString(limits)))
        );
        var memory = (WasmImportInfo.Memory)WasmModule.InspectImports(bytes).Imports.Single();

        // Act
        var description = memory.ToString();

        // Assert
        await Assert.That(description).Contains(memory.Type.ToString());
    }
}
