using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("FC0C00000B", "type mismatch", 0)]
    [Arguments("410041004200FC0C00000B", "type mismatch", 3)]
    [Arguments("FC0E00000B", "type mismatch", 0)]
    [Arguments("410042004100FC0E00000B", "type mismatch", 3)]
    [Arguments("FC11000B", "type mismatch", 0)]
    [Arguments("4100D0704100FC11000B", "type mismatch", 3)]
    [Arguments("4200D06F4100FC11000B", "type mismatch", 3)]
    [Arguments("4100D06F4200FC11000B", "type mismatch", 3)]
    [Arguments("00FC0C01020B", "unknown table 2", 1)]
    [Arguments("00FC0C01000B", "unknown elem segment 1", 1)]
    [Arguments("00FC0C00010B", "type mismatch", 1)]
    [Arguments("00FC0D010B", "unknown elem segment 1", 1)]
    [Arguments("00FC0E02030B", "unknown table 2", 1)]
    [Arguments("00FC0E00020B", "unknown table 2", 1)]
    [Arguments("00FC0E00010B", "type mismatch", 1)]
    [Arguments("00FC11020B", "unknown table 2", 1)]
    public async Task 一括tableの型か添字かoperandが不正_到達不能でも設計順で元命令位置の検証失敗になる(
        string body,
        string prefix,
        int instructionIndex
    )
    {
        // Arrange
        var module = CreateBulkValidationModule(body);

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
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[instructionIndex].ByteOffset,
                        0,
                        10
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    public async Task 到達不能で一括操作の添字と型が有効_全4命令がスタック多相性を維持する()
    {
        // Arrange
        var module = CreateBulkValidationModule("00FC0C0000FC0D00FC0E0000FC11000B");

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    private static WasmModule CreateBulkValidationModule(string body)
    {
        return WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((0x6F, 1, 2), (0x70, 1, 2)),
                HostLinkingModuleBinary.Section(9, [1, 5, 0x6F, 0]),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );
    }
}
