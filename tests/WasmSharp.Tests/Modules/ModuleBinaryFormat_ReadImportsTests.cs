using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Modules.Imports;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryFormat_ReadImportsTests
{
    [Test]
    public async Task 全種類のimport_宣言順と未検証の型と位置を保持する()
    {
        // Arrange
        var section = HostLinkingModuleBinary.Imports(
            ("env", "g", 3, [0x7B, 1]),
            ("", "", 0, HostLinkingModuleBinary.Unsigned(uint.MaxValue)),
            ("env", "m", 2, HostLinkingModuleBinary.Limits(uint.MaxValue, 0)),
            ("env", "t", 1, [0x6F, .. HostLinkingModuleBinary.Limits(3)]),
            ("env", "g", 3, [0x70, 0])
        );

        // Act
        var imports = ReadImports(section[2..]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(imports.Count).IsEqualTo(5);
            await Assert.That(imports[0].Kind).IsEqualTo(WasmExternalKind.Global);
            await Assert.That(imports[0].ModuleName).IsEqualTo("env");
            await Assert.That(imports[0].Name).IsEqualTo("g");
            await Assert.That(imports[0].ByteOffset).IsEqualTo(11L);
            await Assert
                .That(((GlobalImport)imports[0]).Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.V128, true));
            await Assert.That(imports[1].ModuleName).IsEqualTo("");
            await Assert.That(imports[1].Name).IsEqualTo("");
            await Assert.That(((FunctionImport)imports[1]).TypeIndex).IsEqualTo(uint.MaxValue);
            await Assert
                .That(((MemoryImport)imports[2]).Type.Limits)
                .IsEqualTo(new WasmLimits(uint.MaxValue, 0));
            await Assert.That(((MemoryImport)imports[2]).Type.ByteOffset).IsEqualTo(35L);
            await Assert
                .That(((TableImport)imports[3]).Type.ElementKind)
                .IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(((TableImport)imports[3]).Type.Limits).IsEqualTo(new WasmLimits(3));
            await Assert.That(((TableImport)imports[3]).Type.ByteOffset).IsEqualTo(49L);
            await Assert
                .That(((GlobalImport)imports[4]).Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.FuncRef, false));
        }
    }

    [Test]
    [Arguments("0101FF000000", 12L)]
    [Arguments("010001FF0000", 13L)]
    [Arguments("01000004", 13L)]
    [Arguments("0100000080", 15L)]
    [Arguments("010000000080", 15L)]
    [Arguments("010000017F0000", 14L)]
    [Arguments("010000027F", 14L)]
    [Arguments("010000020100", 16L)]
    [Arguments("010000037F02", 15L)]
    [Arguments("010000036E00", 14L)]
    public async Task Import記述が破損_元位置を持つDecode失敗になる(string hex, long offset)
    {
        // Arrange
        var payload = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() => ReadImports(payload))
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, 2));
    }

    private static List<ModuleImport> ReadImports(byte[] payload)
    {
        var reader = new ModuleBinaryReader(payload, 10, 2);
        var imports = ModuleBinaryFormat.ReadImports(ref reader);
        ModuleBinaryFormat.RequireEnd(ref reader);
        return imports;
    }
}
