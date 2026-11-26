using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmImportInfoTable_LimitsTests
{
    [Test]
    [Arguments(false, 5U)]
    [Arguments(true, 5U)]
    [Arguments(true, null)]
    public async Task 調査した型付きテーブルのlimitsを変更する_関数シグネチャとnull可否を保つ(bool isNullable, uint? maximum)
    {
        // Arrange
        var refType = (byte)(isNullable ? 0x63 : 0x64);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7E], [0x7F])),
            HostLinkingModuleBinary.Imports(("env", "callbacks", 1, [refType, 0, 1, 2, 8]))
        );
        var original = (WasmImportInfo.Table)WasmModule.InspectImports(bytes).Imports.Single();
        var expected = WasmValueType.FunctionReference(
            new WasmFunctionType([WasmValueKind.I64], [WasmValueKind.I32]),
            isNullable
        );

        // Act
        var changed = original with { Limits = new WasmLimits(3, maximum) };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(original.Type.ElementValueType).IsEqualTo(expected);
            await Assert.That(original.Limits).IsEqualTo(new WasmLimits(2, 8));
            await Assert.That(changed.Type.ElementValueType).IsEqualTo(expected);
            await Assert.That(changed.Limits).IsEqualTo(new WasmLimits(3, maximum));
            await Assert.That(changed.ToString()).Contains($"ElementType = {expected}");
        }
    }

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
