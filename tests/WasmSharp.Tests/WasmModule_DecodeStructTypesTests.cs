using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("4000", "malformed storage type", 13L)] // 格納型として使用できない符号
    [Arguments("634000", "malformed storage type", 13L)] // 参照先のヒープ型が不正
    [Arguments("7802", "malformed mutability", 14L)] // i8フィールドの可変性が不正
    public async Task 構造体フィールドの符号化が不正である_該当する格納型または可変性の位置を報告する(
        string field,
        string diagnostic,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x5F, 1, .. Convert.FromHexString(field)])
        );

        // Act & Assert
        var error = await Assert
            .That(() => WasmModule.Decode(binary, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith(diagnostic);
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, 1));
        }
    }
}
