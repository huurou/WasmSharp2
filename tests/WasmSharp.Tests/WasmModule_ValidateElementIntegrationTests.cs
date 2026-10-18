using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("01000101", "unknown function", 3)]
    [Arguments("020141000B0000", "unknown table", 0)]
    [Arguments("0042000B00", "type mismatch", 3)]
    [Arguments("060041000B6F00", "type mismatch", 0)]
    [Arguments("057001D06F0B", "type mismatch", 5)]
    [Arguments("07700141000B", "type mismatch", 5)]
    [Arguments("05700123000B", "unknown global", 3)]
    [Arguments("057001D200D1700B", "constant expression required", 5)]
    public async Task 要素の添字や型や定数式が不正_Decodeで保持しValidateで拒否してInstantiateを許可しない(
        string segment,
        string prefix,
        int relativeOffset
    )
    {
        // Arrange
        var section = HostLinkingModuleBinary.Section(9, [1, .. Convert.FromHexString(segment)]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
            HostLinkingModuleBinary.Tables((0x70, 0, null)),
            section
        );
        var module = WasmModule.Decode(bytes);
        var segmentOffset = bytes.Length - section.Length + 3;

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Validate, segmentOffset + relativeOffset, null, 9)
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 要素から定義globalかmutableImportを参照する_定数式として拒否する(
        bool definedGlobal
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            definedGlobal ? [] : HostLinkingModuleBinary.Imports(("env", "g", 3, [0x70, 1])),
            definedGlobal ? HostLinkingModuleBinary.Globals((0x70, false, [0xD0, 0x70, 0x0B])) : [],
            HostLinkingModuleBinary.Section(9, [1, 5, 0x70, 1, 0x23, 0, 0x0B])
        );
        var module = WasmModule.Decode(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        definedGlobal ? "unknown global" : "constant expression required",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.Location.SectionId).IsEqualTo((byte)9);
        }
    }
}
