using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleDecoder_DecodeTests
{
    [Test]
    [Arguments(0u, 0u)]
    [Arguments(33u, 128u)]
    [Arguments(uint.MaxValue, uint.MaxValue)]
    public async Task メモリ命令にu32の即値がある_全命令のalignとoffsetと元位置を意味検査せず保持する(
        uint alignment,
        uint offset
    )
    {
        // Arrange
        var encodedAlignment = HostLinkingModuleBinary.Unsigned(alignment);
        var encodedOffset = HostLinkingModuleBinary.Unsigned(offset);
        var instructions = Enumerable
            .Range(0x28, 23)
            .SelectMany(x =>
                new byte[] { (byte)x }
                    .Concat(encodedAlignment)
                    .Concat(encodedOffset)
            )
            .Append((byte)0x0B)
            .ToArray();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], instructions))
        );
        var firstOffset = bytes.Length - instructions.Length;

        // Act
        var module = ModuleDecoder.Decode(bytes);
        Array.Fill(bytes, (byte)0xFF);

        // Assert
        for (var index = 0; index < 23; index++)
        {
            var decoded = module.Functions[0].Instructions[index];
            using (Assert.Multiple())
            {
                await Assert.That(decoded.Opcode).IsEqualTo(new OpcodeKey(0, (uint)(0x28 + index)));
                await Assert
                    .That(decoded.MemoryArgument)
                    .IsEqualTo(new MemoryArgument(alignment, offset));
                await Assert
                    .That(decoded.ByteOffset)
                    .IsEqualTo(
                        (long)firstOffset
                            + index * (1 + encodedAlignment.Length + encodedOffset.Length)
                    );
            }
        }
    }
}
