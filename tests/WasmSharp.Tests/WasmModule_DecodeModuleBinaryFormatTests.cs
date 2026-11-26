using WasmSharp.Exceptions;
using WasmSharp.Modules.Definitions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    public async Task 上位の関数型を指定する_その型への非null参照を持つ宣言としてデコードする()
    {
        // Arrange
        // 型0は継承可能で、型1は型0を継承するfinalな関数型として宣言する。
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x50, 0, 0x60, 0, 0, 0x4F, 1, 0, 0x60, 0, 0])
        );
        var parent = WasmDefinedType.FromDecodedGroup([new TypeDeclaration([], [], false, [])], firstIndex: 0);
        var parentReference = WasmValueType.DefinedReference(parent, isNullable: false);
        var expected = WasmDefinedType.FromDecodedGroup(
            [new TypeDeclaration([], [], true, [parentReference])],
            firstIndex: 1
        );

        // Act
        var decoded = WasmModule.Decode(binary, WasmSpecVersion.Core3).Types[1];

        // Assert
        await Assert.That(decoded.IsEquivalentTo(expected)).IsTrue();
    }

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
