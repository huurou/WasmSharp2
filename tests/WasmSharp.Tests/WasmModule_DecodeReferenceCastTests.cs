using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments((byte)0x18, (byte)0x04)]
    [Arguments((byte)0x19, (byte)0x80)]
    public async Task キャスト分岐フラグの予約ビットが立っている_デコード時にフラグの位置を報告する(byte opcode, byte flags)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], [0xFB, opcode, flags, 0, 0x6E, 0x6C, 0x0B]))
        );

        // Act & Assert
        var error = await Assert
            .That(() => WasmModule.Decode(binary, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith("malformed br_on_cast flags");
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, binary.Length - 5, 0, 10));
        }
    }
}
