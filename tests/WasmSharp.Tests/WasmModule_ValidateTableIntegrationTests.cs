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
        var executionContext = new WasmExecutionContext();
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((0x6F, 1, 2)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            ),
            WasmSpecVersion.Core2
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal)).IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Validate, module.Functions[0].Instructions[instructionIndex].ByteOffset, 0, 10)
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments((byte)4, "FC1000451A0B", 1)]
    [Arguments((byte)0, "FC1000501A0B", 1)]
    [Arguments((byte)4, "D06F4100FC0F001A0B", 2)]
    [Arguments((byte)0, "D06F4200FC0F001A0B", 2)]
    [Arguments((byte)4, "D06F4200FC0F00451A0B", 3)]
    [Arguments((byte)0, "D06F4100FC0F00501A0B", 3)]
    public async Task 要素数または増分の幅がテーブルのアドレス型と異なる_不一致の命令で検証に失敗する(
        byte tableFlags,
        string body,
        int instructionIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Section(4, [1, 0x6F, tableFlags, 0]),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            ),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert
                .That(exception.Location?.ByteOffset)
                .IsEqualTo(module.Functions[0].Instructions[instructionIndex].ByteOffset);
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
                HostLinkingModuleBinary.Code(([], [0x00, 0x25, 0, 0x26, 0, 0xFC, 0x0F, 0, 0x1A, 0xFC, 0x10, 0, 0x1A, 0x0B]))
            ),
            WasmSpecVersion.Core2
        );

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }
}
