using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 一括table命令を連続Decodeする_2添字の順序と最大uintと命令終端を保持する(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    Convert.FromHexString(
                        "FC8C008100FFFFFFFF0FFC0DFFFFFFFF0FFC0EFFFFFFFF0F8200FC1180000B"
                    )
                )
            )
        );
        using var stream = new MemoryStream(bytes);

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        var instructions = module.Functions[0].Instructions;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instructions.Length).IsEqualTo(5);
            await Assert.That(instructions[0].Opcode).IsEqualTo(new OpcodeKey(0xFC, 0x0C));
            await Assert.That(instructions[0].Index).IsEqualTo(1u);
            await Assert.That(instructions[0].SecondaryIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(instructions[1].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(instructions[2].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(instructions[2].SecondaryIndex).IsEqualTo(2u);
            await Assert.That(instructions[3].Index).IsEqualTo(0u);
            await Assert
                .That(
                    instructions
                        .Select(x => x.ByteOffset - instructions[0].ByteOffset)
                        .SequenceEqual([0L, 10L, 17L, 26L, 30L])
                )
                .IsTrue();
            await Assert.That(instructions[^1].ByteOffset).IsEqualTo((long)bytes.Length - 1);
        }
    }

    [Test]
    [Arguments("FC0CFFFFFFFF10000B", 6, "integer too large")]
    [Arguments("FC0C00FFFFFFFF100B", 7, "integer too large")]
    [Arguments("FC0E808080808000000B", 7, "integer representation too long")]
    [Arguments("FC0E008080808080000B", 8, "integer representation too long")]
    [Arguments("FC0DFFFFFFFF100B", 6, "integer too large")]
    [Arguments("FC11FFFFFFFF100B", 6, "integer too large")]
    [Arguments("FC0C00", 3, "unexpected end of section or function")]
    [Arguments("FC0E0000", 4, "unexpected end of section or function")]
    public async Task 一括tableの即値か式終端が不正_公開Decodeで元の入力位置を保持して拒否する(
        string body,
        int relativeOffset,
        string prefix
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
        );
        var instructionOffset = bytes.Length - body.Length / 2;

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Decode, instructionOffset + relativeOffset, 0, 10)
                );
        }
    }
}
