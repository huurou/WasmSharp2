using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("FD15")]
    [Arguments("FD20")]
    [Arguments("FD0D")]
    [Arguments("FD0D000000000000000000000000000000")]
    public async Task SIMD接続のlaneかshuffle即値が切断する_両入力で即値先頭のDecode診断を返す(
        string instruction
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instruction)))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        foreach (var decode in decoders)
        {
            // Act & Assert
            var exception = await Assert.That(decode).ThrowsExactly<WasmDecodeException>();
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
                    .IsEqualTo(
                        new(WasmProcessingStage.Decode, Array.IndexOf(bytes, (byte)0xFD) + 2, 0, 10)
                    );
            }
        }
    }
}
