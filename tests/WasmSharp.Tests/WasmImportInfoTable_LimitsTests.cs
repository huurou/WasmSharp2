using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmImportInfoTable_LimitsTests
{
    [Test]
    public async Task 従来の32ビットテーブルのimportをコピーして分解する_要素型と上下限を変更できる()
    {
        // Arrange
        var original = new WasmImportInfo.Table(
            ModuleName: "env",
            Name: "t",
            ElementType: WasmValueKind.FuncRef,
            Limits: new WasmLimits(1)
        );
        var replacement = new WasmLimits(2, uint.MaxValue);

        // Act
        var changed = original with { ElementType = WasmValueKind.ExternRef, Limits = replacement };
        var (module, name, kind, limits) = changed;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((module, name, kind, limits)).IsEqualTo(("env", "t", WasmValueKind.ExternRef, replacement));
            await Assert
                .That(changed.Type)
                .IsEqualTo(new WasmTableType(WasmValueKind.ExternRef, WasmValueKind.I32, 2, uint.MaxValue));
            await Assert.That(original.Limits).IsEqualTo(new WasmLimits(1));
            await Assert
                .That(original.ToString())
                .IsEqualTo(
                    "Table { ModuleName = env, Name = t, Kind = Table, ElementType = FuncRef, Limits = WasmLimits { Minimum = 1, Maximum =  } }"
                );
        }
    }

    [Test]
    public async Task 調査した64ビットテーブルのコピーに32ビットのlimitsを指定する_アドレス型を変更せず拒否する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Imports(("env", "t", 1, [0x70, 0x04, 1])));
        var table = (WasmImportInfo.Table)WasmModule.InspectImports(bytes).Imports.Single();

        // Act & Assert
        var exception = await Assert
            .That(() => table with { Limits = new WasmLimits(1) })
            .ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains("64ビット");
            await Assert.That(exception.Message).Contains(nameof(WasmImportInfo.Table.Limits));
        }
    }

    [Test]
    public async Task コピーで要素数の上下限にnullを指定する_引数例外を返す()
    {
        // Arrange
        var table = new WasmImportInfo.Table("env", "t", WasmValueKind.FuncRef, new WasmLimits(1));

        // Act & Assert
        await Assert.That(() => table with { Limits = null! }).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("700400")]
    [Arguments("6F008080808010")]
    [Arguments("7001008080808010")]
    public async Task アドレス型または要素数を32ビットのlimitsで表せない_Limitsの取得を拒否してTypeを表示する(string type)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "t", 1, Convert.FromHexString(type)))
        );
        var table = (WasmImportInfo.Table)WasmModule.InspectImports(bytes).Imports.Single();

        // Act & Assert
        var exception = await Assert.That(() => table.Limits).ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(nameof(WasmImportInfo.Table.Type));
            await Assert.That(table.ToString()).Contains(table.Type.ToString());
        }
    }
}
