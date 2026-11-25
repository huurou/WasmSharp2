using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(WasmSpecVersion.Core2, "014E01600000", "malformed function type", 11L)]
    [Arguments(WasmSpecVersion.Core2, "014F00600000", "malformed function type", 11L)]
    [Arguments(WasmSpecVersion.Core2, "015000600000", "malformed function type", 11L)]
    [Arguments(WasmSpecVersion.Core3, "014F005000600000", "malformed definition type", 13L)]
    [Arguments(WasmSpecVersion.Core3, "014E014E00", "malformed definition type", 13L)]
    public async Task 仕様上許可されない位置に型宣言がある_Decodeが該当する型の位置を報告する(
        WasmSpecVersion version,
        string section,
        string diagnostic,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(1, Convert.FromHexString(section)));

        // Act & Assert
        var exception = await Assert.That(() => WasmModule.Decode(binary, version)).ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, 1));
        }
    }

    [Test]
    public async Task 同じ関数型を複数の型番号で宣言する_番号順を保ち参照先とnull可否まで一致した型を共有する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    6,
                    .. Convert.FromHexString("6000017F"), // 0: () -> i32
                    .. Convert.FromHexString("6000017E"), // 1: () -> i64
                    .. Convert.FromHexString("6000017F"), // 2: () -> i32
                    .. Convert.FromHexString("60016400016301"), // 3: (ref 0) -> (ref null 1)
                    .. Convert.FromHexString("60016402016301"), // 4: (ref 2) -> (ref null 1)
                    .. Convert.FromHexString("60016302016301"), // 5: (ref null 2) -> (ref null 1)
                ]
            )
        );

        // Act
        var types = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Types;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(types.Length).IsEqualTo(6);
            await Assert.That(types[0].Results.Single()).IsEqualTo(WasmValueKind.I32);
            await Assert.That(types[1].Results.Single()).IsEqualTo(WasmValueKind.I64);
            await Assert.That(types[2]).IsSameReferenceAs(types[0]);
            await Assert.That(types[3].ParameterTypes.Single().FunctionType).IsSameReferenceAs(types[0]);
            await Assert.That(types[3].ResultTypes.Single().FunctionType).IsSameReferenceAs(types[1]);
            await Assert.That(types[4]).IsSameReferenceAs(types[3]);
            await Assert.That(types[5].ParameterTypes.Single().IsNullable).IsTrue();
            await Assert.That(types[5]).IsNotSameReferenceAs(types[3]);
        }
    }

    [Test]
    [Arguments(WasmSpecVersion.Core2, "700400", "integer too large", 12L)]
    [Arguments(WasmSpecVersion.Core2, "70008080808010", "integer too large", 17L)]
    [Arguments(WasmSpecVersion.Core2, "7000808080808000", "integer representation too long", 18L)]
    [Arguments(WasmSpecVersion.Core3, "7004FFFFFFFFFFFFFFFFFF02", "integer too large", 22L)]
    public async Task テーブル宣言の符号化が不正である_選択した仕様版のデコード診断と位置を返す(
        WasmSpecVersion version,
        string type,
        string diagnostic,
        long offset
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(4, [1, .. Convert.FromHexString(type)]));

        // Act & Assert
        var exception = await Assert.That(() => WasmModule.Decode(bytes, version)).ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, 4));
        }
    }

    [Test]
    [Arguments(WasmSpecVersion.Core2, "0503010400", "integer too large")]
    [Arguments(WasmSpecVersion.Core2, "050701008080808010", "integer too large")]
    [Arguments(WasmSpecVersion.Core3, "050C010082808080808080808070", "integer too large")]
    public async Task メモリ宣言の符号化が不正である_選択した仕様版のデコード診断を返す(
        WasmSpecVersion specVersion,
        string section,
        string expected
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(Convert.FromHexString(section));

        // Act & Assert
        var exception = await Assert.That(() => WasmModule.Decode(bytes, specVersion)).ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(expected);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.Location.SectionId).IsEqualTo((byte)5);
        }
    }

    [Test]
    [Arguments((byte)4, "700200", 12L, "malformed limits flags: テーブルのlimitsフラグが不正です。")]
    [Arguments((byte)5, "0200", 11L, "malformed limits flags: メモリのlimitsフラグが不正です。")]
    public async Task Core3でlimitsフラグが不正である_対象の名前を含む診断とフラグの位置を返す(
        byte sectionId,
        string type,
        long offset,
        string expected
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(sectionId, [1, .. Convert.FromHexString(type)])
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).IsEqualTo(expected);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, sectionId));
        }
    }

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
    public async Task Core3で未対応命令を含む関数本体の数が宣言と異なる_関数件数の不一致を報告する()
    {
        // Arrange
        var codeSection = HostLinkingModuleBinary.Code(([], [0xD3, 0x0B]));
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0, 0),
            codeSection
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("function and code section have inconsistent lengths");
            var location = exception.Location!;
            // 未対応命令で読み取りを中断した時点で、codeセクションの件数不一致が確定する。
            await Assert.That(location.Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(location.SectionId).IsEqualTo((byte)10);
            await Assert.That(location.FunctionIndex).IsNull();
            await Assert.That(location.ByteOffset!.Value).IsBetween(bytes.LongLength - codeSection.Length, bytes.LongLength);
        }
    }

    [Test]
    public async Task Core3のデータセグメントのオフセット式に未対応命令があり件数が一致する_命令名と位置を報告する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Section(11, [1, 0, 0xD3, 0x0B, 0])
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("ref.eq");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, bytes.Length - 3, null, 11));
        }
    }

    [Test]
    public async Task Core3のデータセグメントのオフセット式に未対応命令があり件数が異なる_件数の不一致を報告する()
    {
        // Arrange
        var dataSection = HostLinkingModuleBinary.Section(11, [1, 0, 0xD3, 0x0B, 0]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Section(12, [2]),
            dataSection
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("data count and data section have inconsistent lengths");
            var location = exception.Location!;
            await Assert.That(location.Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(location.SectionId).IsEqualTo((byte)11);
            await Assert.That(location.FunctionIndex).IsNull();
            await Assert.That(location.ByteOffset!.Value).IsBetween(bytes.LongLength - dataSection.Length, bytes.LongLength);
        }
    }

    [Test]
    [Arguments("03030200000A0601010001D30B", 0u, (byte)10, 3)]
    [Arguments("0C01020B02010001D30B00", null, (byte)11, 4)]
    public async Task Core3の宣言範囲を越えた後に未対応命令がある_先に検出した境界の診断と位置を報告する(
        string sections,
        uint? functionIndex,
        byte sectionId,
        int distanceFromEnd
    )
    {
        // Arrange
        // 関数本体はlocalsの件数の直後、データセクションはフラグの直後で宣言範囲が終わる。
        // それぞれ範囲外にnopと未対応のref.eqを置き、境界の位置と未対応命令の位置を区別する。
        var bytes = HostLinkingModuleBinary.Create(Convert.FromHexString(sections));

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unexpected end of section or function");
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(
                        WasmProcessingStage.Decode,
                        bytes.Length - distanceFromEnd,
                        functionIndex,
                        sectionId
                    )
                );
        }
    }

    [Test]
    public async Task Core3のグローバル初期化式にstructNewがある_未対応の命令名を報告する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Globals((0x6F, false, [0xFB, 0x00, 0, 0x0B])));

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo("struct.new");
    }

    [Test]
    [Arguments("6E")] // anyref
    [Arguments("646C")] // (ref i31)
    [Arguments("71")] // nullref
    public async Task Core2の関数型にGC参照型がある_参照型のデコードエラーを報告する(string encodedType)
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x60, 1, .. Convert.FromHexString(encodedType), 0])
        );

        // Act & Assert
        var error = await Assert
            .That(() => WasmModule.Decode(binary, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(error!.Message).StartsWith("malformed reference type");
    }

    [Test]
    [Arguments(WasmSpecVersion.Core2, "1400", "illegal opcode 14: Core 2.0")]
    [Arguments(WasmSpecVersion.Core3, "FD9A01", "illegal opcode 9a: Core 3.0")]
    [Arguments(WasmSpecVersion.Core3, "FB1F", "illegal opcode fb 1f: Core 3.0")]
    [Arguments(WasmSpecVersion.Core2, "D3", "illegal opcode d3: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "D4", "illegal opcode d4: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "D500", "illegal opcode d5: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "D600", "illegal opcode d6: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "FB", "illegal opcode fb: Core 2.0")]
    [Arguments(WasmSpecVersion.Core2, "FB1C", "illegal opcode fb: Core 2.0")]
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
    public async Task ストリームがnullである_引数名を示して拒否する(bool specifyVersion)
    {
        // Arrange
        Stream stream = null!;

        // Act & Assert
        var exception = await Assert
            .That(() => specifyVersion ? WasmModule.Decode(stream, WasmSpecVersion.Core2) : WasmModule.Decode(stream))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("stream");
    }

    [Test]
    public async Task ストリームがnullで仕様版も未定義である_仕様版の引数を先に拒否する()
    {
        // Arrange
        Stream stream = null!;

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(stream, (WasmSpecVersion)2))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(exception!.ParamName).IsEqualTo("specVersion");
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
    public async Task 再帰関数型を持つタグをCore3でimportする_デコード段階でタグの未実装を報告する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [1, 0x4E, 1, 0x60, 1, 0x63, 0, 0]), // (rec (type $t (func (param (ref null $t)))))
            HostLinkingModuleBinary.Imports(("env", "event", 4, [0, 0]))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception?.Feature).IsEqualTo("tag import");
            await Assert
                .That(exception?.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, bytes.Length - 3, null, 2));
        }
    }

    [Test]
    public async Task Core3でタグのexport宣言を読む_タグが未対応であることと種類の位置を返す()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Exports(("event", 4, 0)));

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception?.Feature).IsEqualTo("tag export");
            await Assert
                .That(exception?.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, bytes.Length - 2, null, 7));
        }
    }

    [Test]
    [Arguments("020401000005", "malformed import kind")]
    [Arguments("070401000500", "malformed export kind")]
    public async Task Core3のimportまたはexportで予約された種類を指定する_種類の構文エラーを報告する(
        string section,
        string diagnostic
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(Convert.FromHexString(section));

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core3))
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Message).StartsWith(diagnostic);
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
