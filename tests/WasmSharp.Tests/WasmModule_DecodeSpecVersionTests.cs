using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("D3", "ref.eq")]
    [Arguments("FB0F", "array.len")]
    [Arguments("FD8002", "i8x16.relaxed_swizzle")]
    public async Task Core3の関数本体に未対応の割当がある_その命令名を報告する(string opcode, string name)
    {
        // Arrange
        // unreachableの後はどの入力型でも検証できる。結果をdropし、関数の結果型i32を返す。
        var bytes = ConstantModuleBinary.Create(0x7F, [0x00, .. Convert.FromHexString(opcode), 0x1A, 0x41, 0, 0x0B]);

        // Act & Assert
        var unsupported = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(unsupported!.Feature).IsEqualTo(name);
    }

    [Test]
    public async Task Core3のグローバル初期化式にrefI31がある_未対応の命令名を報告する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((0x6F, false, [0x41, 1, 0xFB, 0x1C, 0xFB, 0x1B, 0x0B]))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo("ref.i31");
    }

    [Test]
    [Arguments(WasmSpecVersion.Core3, "FD9A01", "illegal opcode 9a: Core 3.0")]
    [Arguments(WasmSpecVersion.Core3, "FB1F", "illegal opcode fb 1f: Core 3.0")]
    [Arguments(WasmSpecVersion.Core2, "D3", "illegal opcode d3: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "FB", "illegal opcode fb: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "FD8002", "illegal opcode 100: Core 2.0")]
    public async Task 選択した仕様で未割当の命令番号がある_その版のデコードエラーを報告する(
        WasmSpecVersion specVersion,
        string opcode,
        string diagnostic
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(opcode));

        // Act & Assert
        var exception = await Assert.That(() => WasmModule.Decode(bytes, specVersion)).ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Message).StartsWith(diagnostic);
    }

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
    [Arguments(false)]
    [Arguments(true)]
    public async Task バイト列またはストリームで版を省略する_検証後も既定のCore3を保持する(bool useStream)
    {
        // Arrange
        // 初期化式のi32.addはCore 3.0で使用できる。
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 1, 0x41, 2, 0x6A, 0x0B]))
        );
        using var stream = new MemoryStream(bytes);

        // Act
        var module = (useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes)).Validate();

        // Assert
        await Assert.That(module.SpecVersion).IsEqualTo(WasmSpecVersion.Core3);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 未定義の版を指定_入力を読まずに引数の範囲外として拒否する(bool useStream)
    {
        // Arrange
        byte[] bytes = [];
        using var stream = new ThrowingReadStream(new IOException());
        var invalidVersion = (WasmSpecVersion)2;

        // Act & Assert
        var exception = await Assert
            .That(() => useStream ? WasmModule.Decode(stream, invalidVersion) : WasmModule.Decode(bytes, invalidVersion))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("specVersion");
            await Assert.That(exception.ActualValue).IsEqualTo(invalidVersion);
            await Assert.That(exception.Message).Contains("仕様の版");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 公式のタグsectionをCore2でデコード_従来どおりsection_IDの不正として拒否する(bool useStream)
    {
        // Arrange
        var bytes = Core3OfficialBinary.Tag;
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() =>
                useStream
                    ? WasmModule.Decode(stream, WasmSpecVersion.Core2)
                    : WasmModule.Decode(bytes, WasmSpecVersion.Core2)
            )
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
