using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 間接callが非最短LEBとuint全域の二添字を持つ_元位置と型tableの順序を保持する(
        bool useStream
    )
    {
        // Arrange
        byte[] body = [0x11, 0x80, 1, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x11, 0x80, 0, 0x81, 0, 0x0B];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], body))
        );
        using var stream = new MemoryStream(bytes);

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    module
                        .Functions[0]
                        .Instructions.Select(x => x.Index)
                        .SequenceEqual(new uint[] { 128, 0, 0 })
                )
                .IsTrue();
            await Assert
                .That(
                    module
                        .Functions[0]
                        .Instructions.Select(x => x.SecondaryIndex)
                        .SequenceEqual(new uint[] { uint.MaxValue, 1, 0 })
                )
                .IsTrue();
            await Assert
                .That(
                    module
                        .Functions[0]
                        .Instructions.Select(x => x.ByteOffset)
                        .SequenceEqual(
                            new long[]
                            {
                                bytes.Length - body.Length,
                                bytes.Length - 6,
                                bytes.Length - 1,
                            }
                        )
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("11", "unexpected end", 1)]
    [Arguments("1180", "unexpected end", 2)]
    [Arguments("110080", "unexpected end", 3)]
    [Arguments("118080808010", "integer too large", 5)]
    [Arguments("11008080808010", "integer too large", 6)]
    [Arguments("118080808080", "integer representation too long", 6)]
    [Arguments("11008080808080", "integer representation too long", 7)]
    public async Task 間接callのどちらかの添字が破損している_元入力位置付きのDecode失敗になる(
        string body,
        string prefix,
        int relativeOffset
    )
    {
        // Arrange
        var instructions = Convert.FromHexString(body);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], instructions))
        );

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
                    new(
                        WasmProcessingStage.Decode,
                        bytes.Length - instructions.Length + relativeOffset,
                        0,
                        10
                    )
                );
        }
    }
}
