using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments(0x7B, false, "select.v128")]
    [Arguments(0x7B, true, "select.typed.v128")]
    [Arguments(0x70, true, "select.typed.funcref")]
    [Arguments(0x6F, true, "select.typed.externref")]
    public async Task 後続型のselectがその場の型検証に成功_ValidateのFeatureと未完了範囲を保持し部分コードを公開しない(
        int valueType,
        bool typed,
        string feature
    )
    {
        // Arrange
        var kind = (byte)valueType;
        byte[] select = typed ? [0x1C, 1, kind] : [0x1B];
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([kind, kind, 0x7F], [])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Code(
                ([], [0x0B]),
                ([], [0x20, 0, 0x20, 1, 0x20, 2, .. select, 0x1A, 0x42, 0, 0x0B])
            )
        );
        var module = WasmModule.Decode(binary);
        var definition = module.Functions[1];
        var byteOffset = definition.Instructions[3].ByteOffset;

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, byteOffset, 2, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(byteOffset);
            await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(binary.LongLength);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
            await Assert.That(exception.UnverifiedRanges[1].EndOffset).IsEqualTo(binary.LongLength);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
            await Assert.That(module.Functions[1]).IsSameReferenceAs(definition);
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments(0x7B, "select.typed.v128")]
    [Arguments(0x70, "select.typed.funcref")]
    [Arguments(0x6F, "select.typed.externref")]
    public async Task typed_selectの両候補がUnknown_明示注釈で分かる後続機能をUnsupportedとして通知する(
        int valueType,
        string feature
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], [0, 0x1C, 1, (byte)valueType, 0x1A, 0x0B]))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[1].ByteOffset,
                        0,
                        10
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }
}
