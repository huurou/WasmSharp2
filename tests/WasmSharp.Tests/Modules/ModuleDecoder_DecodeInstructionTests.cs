using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleDecoder_DecodeTests
{
    [Test]
    [Arguments((byte)0x10)]
    [Arguments((byte)0x20)]
    [Arguments((byte)0x21)]
    [Arguments((byte)0x22)]
    [Arguments((byte)0x23)]
    [Arguments((byte)0x24)]
    public async Task 添字付き命令がある_uint全域の添字と命令位置を保持する(byte opcode)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(
                ([], [opcode, 0, opcode, 0x80, 1, opcode, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x0B])
            )
        );

        // Act
        var module = WasmModule.Decode(bytes);
        var instructions = module.Functions[0].Instructions;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    instructions
                        .Select(x => x.Index)
                        .SequenceEqual(new uint[] { 0, 128, uint.MaxValue, 0 })
                )
                .IsTrue();
            await Assert
                .That(instructions.Take(3).All(x => x.Opcode == new OpcodeKey(0, opcode)))
                .IsTrue();
            await Assert
                .That(
                    instructions
                        .Select(x => x.ByteOffset)
                        .SequenceEqual([
                            bytes.Length - 12,
                            bytes.Length - 10,
                            bytes.Length - 7,
                            bytes.Length - 1,
                        ])
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("1080", 0)]
    [Arguments("2080", 0)]
    [Arguments("2180", 0)]
    [Arguments("2280", 0)]
    [Arguments("2380", 0)]
    [Arguments("2480", 0)]
    [Arguments("10FFFFFFFF1F", 1)]
    public async Task 添字即値が破損している_元関数添字を持つDecode失敗になる(
        string instructions,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - offsetFromEnd, 1, 10));
    }

    [Test]
    public async Task 有効なcall即値の後にメモリ即値が切断される_後続命令のDecode境界位置を返す()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], [0x10, 0x80, 1, 0xFD, 0]))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "unexpected end of section or function",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length, 0, 10));
        }
    }
}
