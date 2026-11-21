using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmImportInfoMemory_LimitsTests
{
    [Test]
    public async Task 調査した32ビットメモリを別のlimitsでコピーする_コピーの要求型だけを変更する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])));
        var original = (WasmImportInfo.Memory)WasmModule.InspectImports(bytes).Imports.Single();

        // Act
        var changed = original with { Limits = new WasmLimits(2, 3) };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(original).IsEqualTo(new WasmImportInfo.Memory("env", "m", new WasmLimits(1)));
            await Assert.That(changed.Limits).IsEqualTo(new WasmLimits(2, 3));
            await Assert.That(changed.Type).IsEqualTo(new WasmMemoryType(WasmValueKind.I32, 2, 3));
        }
    }

    [Test]
    [Arguments("0400")]
    [Arguments("0480808080808040")]
    [Arguments("008080808010")]
    public async Task 調査した要求型を32ビットのlimitsで表せない_取得時に例外を返す(string limits)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "m", 2, Convert.FromHexString(limits)))
        );
        var memory = (WasmImportInfo.Memory)WasmModule.InspectImports(bytes).Imports.Single();

        // Act & Assert
        await Assert.That(() => memory.Limits).ThrowsExactly<InvalidOperationException>();
    }
}
