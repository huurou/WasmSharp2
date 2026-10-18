using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("25001A0B", "type mismatch", 0)]
    [Arguments("410026000B", "type mismatch", 1)]
    [Arguments("D06F410026000B", "type mismatch", 2)]
    [Arguments("4100D07026000B", "type mismatch", 2)]
    [Arguments("D0704100FC0F001A0B", "type mismatch", 2)]
    [Arguments("D06F4200FC0F001A0B", "type mismatch", 2)]
    [Arguments("FC0F001A0B", "type mismatch", 0)]
    [Arguments("0025011A0B", "unknown table 1", 1)]
    [Arguments("0026010B", "unknown table 1", 1)]
    [Arguments("00FC0F011A0B", "unknown table 1", 1)]
    [Arguments("00FC10011A0B", "unknown table 1", 1)]
    public async Task 基本table命令のoperandまたは静的添字が不正_到達不能でも元命令位置で検証失敗する(
        string body,
        string prefix,
        int instructionIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((0x6F, 1, 2)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

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
    public async Task 到達不能でtable添字が有効_基本4命令のoperandにスタック多相性を適用する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((0x6F, 1, 2)),
                HostLinkingModuleBinary.Code(
                    ([], [0x00, 0x25, 0, 0x26, 0, 0xFC, 0x0F, 0, 0x1A, 0xFC, 0x10, 0, 0x1A, 0x0B])
                )
            )
        );

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }
}
