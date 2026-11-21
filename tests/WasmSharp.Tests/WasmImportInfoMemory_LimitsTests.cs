using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmImportInfoMemory_LimitsTests
{
    [Test]
    [Arguments("0001", 1UL)]
    [Arguments("008080808010", 1UL << 32)]
    public async Task 調査した32ビットメモリを別のlimitsでコピーする_コピーの要求型だけを変更する(
        string declaration,
        ulong minimumPages
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "m", 2, Convert.FromHexString(declaration)))
        );
        var original = (WasmImportInfo.Memory)WasmModule.InspectImports(bytes).Imports.Single();

        // Act
        var changed = original with { Limits = new WasmLimits(2, 3) };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(original.Type).IsEqualTo(new WasmMemoryType(WasmValueKind.I32, minimumPages));
            await Assert.That(changed.Limits).IsEqualTo(new WasmLimits(2, 3));
            await Assert.That(changed).IsEqualTo(new WasmImportInfo.Memory("env", "m", new WasmLimits(2, 3)));
        }
    }

    [Test]
    public async Task 調査した64ビットメモリのコピーに32ビット用のlimitsを指定する_設定時に拒否する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Imports(("env", "m", 2, [4, 1])));
        var memory = (WasmImportInfo.Memory)WasmModule.InspectImports(bytes).Imports.Single();

        // Act & Assert
        await Assert.That(() => memory with { Limits = new WasmLimits(2, 3) }).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task コピーにnullのlimitsを指定する_引数例外として拒否する()
    {
        // Arrange
        var memory = new WasmImportInfo.Memory("env", "m", new WasmLimits(1));

        // Act & Assert
        await Assert.That(() => memory with { Limits = null! }).ThrowsExactly<ArgumentNullException>();
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
