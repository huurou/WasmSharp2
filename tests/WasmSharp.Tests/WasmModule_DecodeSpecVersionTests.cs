using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    public async Task Core3を選択してデコード_検証後のmoduleが同じ仕様版を保持する()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", 0)]);

        // Act
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();

        // Assert
        await Assert.That(module.SpecVersion).IsEqualTo(WasmSpecVersion.Core3);
    }

    [Test]
    public async Task 版を省略してデコード_既定のCore2を保持する()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create([(0x7F, [0x41, 0x01, 0x0B])], [("run", 0)]);

        // Act
        var module = WasmModule.Decode(bytes).Validate();

        // Assert
        await Assert.That(module.SpecVersion).IsEqualTo(WasmSpecVersion.Core2);
    }

    [Test]
    public async Task 未定義の版を指定_デコードせず引数の範囲外として拒否する()
    {
        // Arrange
        byte[] bytes = [];

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, (WasmSpecVersion)2))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("specVersion");
            await Assert.That(exception.ActualValue).IsEqualTo((WasmSpecVersion)2);
            await Assert.That(exception.Message).Contains("仕様の版");
        }
    }

    [Test]
    public async Task 公式のタグsectionをCore2でデコード_従来どおりsection_IDの不正として拒否する()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(Core3OfficialBinary.Tag, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("malformed section id");
            await Assert.That(exception.Message).Contains("Core 2.0");
            await Assert.That(exception.Location!.ByteOffset).IsEqualTo(23);
        }
    }

    [Test]
    public async Task 公式のタグsectionをCore3でデコード_タグの未実装をデコード段階で明示する()
    {
        // Arrange
        var bytes = Core3OfficialBinary.Tag;

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("tag section");
            await Assert.That(exception.Message).Contains("未実装");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 23, null, 13));
            await Assert
                .That(exception.UnverifiedRanges.Select(x => (x.Stage, x.StartOffset, x.EndOffset)))
                .IsEquivalentTo([
                    (WasmProcessingStage.Decode, 8L, (long)bytes.Length),
                    (WasmProcessingStage.Validate, 0L, (long)bytes.Length),
                ]);
            await Assert
                .That(exception.UnverifiedRanges.Single(x => x.Stage == WasmProcessingStage.Decode).Description)
                .Contains("外枠");
            await Assert
                .That(exception.UnverifiedRanges.Single(x => x.Stage == WasmProcessingStage.Validate).Description)
                .Contains("入力全体");
        }
    }

    [Test]
    public async Task 初期化式付きtableの後にタグsectionがある公式入力をCore3でデコード_先行payloadの構文で拒否せずタグの未実装を明示する()
    {
        // Arrange
        var bytes = Core3OfficialBinary.TagAfterTableInitializer;

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("tag section");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 30, null, 13));
            await Assert
                .That(exception.UnverifiedRanges.Any(x => x is { Stage: WasmProcessingStage.Decode, StartOffset: 8 }))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(new byte[] { 0x0D, 0x01, 0x00, 0x0E, 0x00 }, "malformed section id")]
    [Arguments(new byte[] { 0x0D, 0x01, 0x00, 0x05, 0x01, 0x00 }, "unexpected content after last section")]
    [Arguments(new byte[] { 0x0D, 0x01, 0x00, 0x0D, 0x01, 0x00 }, "unexpected content after last section")]
    [Arguments(new byte[] { 0x0D, 0x01, 0x00, 0x06, 0x02, 0x00 }, "unexpected end")]
    [Arguments(new byte[] { 0x0D, 0x04, 0x01, 0x00, 0x00 }, "unexpected end")]
    [Arguments(
        new byte[] { 0x06, 0x06, 0x01, 0x7F, 0x00, 0x41, 0x00, 0x0B, 0x0D, 0x03, 0x01, 0x00, 0x00 },
        "unexpected content after last section"
    )]
    [Arguments(new byte[] { 0x0E, 0x00 }, "malformed section id")]
    public async Task 外枠が不正なsectionをCore3でデコード_未実装へ読み替えずデコード失敗にする(
        byte[] sections,
        string prefix
    )
    {
        // Arrange
        byte[] bytes = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00, .. sections];

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(prefix);
            if (prefix == "malformed section id")
            {
                await Assert.That(exception.Message).Contains("Core 3.0");
            }
        }
    }

    [Test]
    [Arguments(new byte[] { 0x0E, 0x00 })]
    [Arguments(new byte[] { 0x0D })]
    public async Task タグを確認する前に外枠の不正を見つける_それより先にある型sectionの構文エラーを報告する(
        byte[] trailingSection
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, [1, 0x61]), trailingSection);

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("malformed function type");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 11, null, 1));
        }
    }
}
