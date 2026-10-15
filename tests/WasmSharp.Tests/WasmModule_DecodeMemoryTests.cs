using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("3F", "unexpected end", 0)]
    [Arguments("40", "unexpected end", 0)]
    [Arguments("3F01", "zero byte expected", 1)]
    [Arguments("4001", "zero byte expected", 1)]
    [Arguments("3F8000", "zero byte expected", 2)]
    [Arguments("408000", "zero byte expected", 2)]
    [Arguments("3F8080808000", "zero byte expected", 5)]
    [Arguments("408080808000", "zero byte expected", 5)]
    public async Task メモリサイズ命令の予約バイトが不正か欠落する_両Decode入口が構文診断と位置を返す(
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        foreach (var useStream in new[] { false, true })
        {
            using var stream = new MemoryStream(bytes);

            // Act & Assert
            var exception = await Assert
                .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                .ThrowsExactly<WasmDecodeException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(WasmProcessingStage.Decode, bytes.Length - offsetFromEnd, 0, 10)
                    );
            }
        }
    }

    [Test]
    [Arguments("28", "unexpected end", 0)]
    [Arguments("2880", "unexpected end", 0)]
    [Arguments("2800", "unexpected end", 0)]
    [Arguments("280080", "unexpected end", 0)]
    [Arguments("28808080808000", "integer representation too long", 1)]
    [Arguments("2800808080808000", "integer representation too long", 1)]
    [Arguments("28FFFFFFFF1F00", "integer too large", 2)]
    [Arguments("2800FFFFFFFF1F", "integer too large", 1)]
    [Arguments("28FF05", "unexpected end", 0)]
    public async Task メモリ即値のLEBが不正か途中で途切れる_両Decode入口が構文診断と元位置を返す(
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        foreach (var useStream in new[] { false, true })
        {
            using var stream = new MemoryStream(bytes);

            // Act & Assert
            var exception = await Assert
                .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                .ThrowsExactly<WasmDecodeException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(WasmProcessingStage.Decode, bytes.Length - offsetFromEnd, 0, 10)
                    );
            }
        }
    }
}
