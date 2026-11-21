using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmImportInfoMemory_ToStringTests
{
    [Test]
    public async Task 従来の32ビットメモリのimport情報を文字列にする_名前と種類とlimitsを表示する()
    {
        // Arrange
        var memory = new WasmImportInfo.Memory("env", "m", new WasmLimits(1, 2));

        // Act
        var description = memory.ToString();

        // Assert
        await Assert
            .That(description)
            .IsEqualTo(
                "Memory { ModuleName = env, Name = m, Kind = Memory, Limits = WasmLimits { Minimum = 1, Maximum = 2 } }"
            );
    }

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
