using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(
        WasmSpecVersion.Core2,
        (byte)1,
        "0160016500",
        "malformed reference type: Core 2.0に存在しない値型です。",
        13L
    )]
    [Arguments(WasmSpecVersion.Core2, (byte)4, "01650000", "malformed reference type: 参照型が不正です。", 11L)]
    [Arguments(WasmSpecVersion.Core3, (byte)1, "0160016500", "malformed reference type: 参照型が不正です。", 13L)]
    [Arguments(WasmSpecVersion.Core3, (byte)1, "016001634000", "malformed heap type: ヒープ型が不正です。", 14L)]
    [Arguments(
        WasmSpecVersion.Core3,
        (byte)1,
        "01600163F07F00",
        "integer representation too long: 抽象ヒープ型は1バイトで符号化します。",
        15L
    )]
    public async Task 関数の引数型またはテーブルの参照型が不正である_仕様版と宣言に対応するデコード診断と位置を返す(
        WasmSpecVersion version,
        byte sectionId,
        string payload,
        string diagnostic,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(sectionId, Convert.FromHexString(payload))
        );

        // Act & Assert
        var failure = await Assert.That(() => WasmModule.Decode(binary, version)).ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure!.Message).IsEqualTo(diagnostic);
            await Assert
                .That(failure.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, sectionId));
        }
    }
}
