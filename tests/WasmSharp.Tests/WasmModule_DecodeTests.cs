using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_DecodeTests
{
    [Test]
    [Arguments("006173", 3L, null, null)]
    [Arguments("0062736D01000000", 1L, null, null)]
    [Arguments("0061736D02000000", 4L, null, null)]
    [Arguments("0061736D01000000010100010100", 11L, null, (byte)1)]
    [Arguments("0061736D01000000070100010100", 11L, null, (byte)1)]
    [Arguments("0061736D010000000D00", 8L, null, (byte)13)]
    [Arguments("0061736D0100000001FFFFFFFF0F", 14L, null, (byte)1)]
    [Arguments("0061736D0100000001020000", 11L, null, (byte)1)]
    [Arguments("0061736D01000000000201FF", 11L, null, (byte)0)]
    [Arguments("0061736D0100000007050101FF0000", 12L, null, (byte)7)]
    [Arguments("0061736D01000000030201000A0100", 14L, null, (byte)10)]
    [Arguments("0061736D01000000030201000A03010100", 17L, 0u, (byte)10)]
    [Arguments("0061736D01000000030201000A050103000B0B", 18L, 0u, (byte)10)]
    [Arguments("0061736D01000000030201000A0C010A02FFFFFFFF0F7F017E0B", 23L, 0u, (byte)10)]
    [Arguments("0061736D010000000202FF", 10L, null, (byte)2)]
    [Arguments("0061736D010000000A01000C0100", 11L, null, (byte)12)]
    public async Task 両入力で構文違反が確定する_未実装に置き換えずDecodeの位置を通知する(
        string hex,
        long offset,
        uint? functionIndex,
        byte? sectionId
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        // Act & Assert
        foreach (var decode in decoders)
        {
            var exception = await Assert.That(decode).ThrowsExactly<WasmDecodeException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, functionIndex, sectionId));
        }
    }

    [Test]
    [Arguments("FF", 33L)]
    [Arguments("D3", 33L)]
    [Arguments("FC12", 33L)]
    [Arguments("FD9A01", 33L)]
    [Arguments("FD8002", 33L)]
    [Arguments("FCFFFFFFFF10", 38L)]
    [Arguments("FD808080808000", 38L)]
    [Arguments("4180808080080B", 38L)]
    [Arguments("056A", 33L)]
    public async Task 両入力で未割当命令や不正LEBや平坦elseがある_命令位置付きの破損になる(
        string instructions,
        long offset
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(instructions));
        using var stream = new MemoryStream(bytes);
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        // Act & Assert
        foreach (var decode in decoders)
        {
            var exception = await Assert.That(decode).ThrowsExactly<WasmDecodeException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 0, 10));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 未対応sectionの後に破損がある_両入力で構文と検証の未確認範囲を通知する(
        bool useStream
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D010000000201000D00");
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("section.import");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 8, null, 2));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(8L);
            await Assert
                .That(exception.UnverifiedRanges[0].EndOffset)
                .IsEqualTo((long)bytes.Length);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
            await Assert
                .That(exception.UnverifiedRanges[1].EndOffset)
                .IsEqualTo((long)bytes.Length);
        }
    }

    [Test]
    public async Task バイト列を公開Decodeに渡す_静的モジュールを返す()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, 0x41, 0x2A, 0x0B);

        // Act
        var module = WasmModule.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.InputLength).IsEqualTo((long)bytes.Length);
            await Assert.That(module.Functions.Length).IsEqualTo(1);
            await Assert.That(module.FunctionCodes.Length).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(8192)]
    public async Task 非seekでshortReadする_現在位置からEOFまで読み入力を閉じない(int chunkSize)
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, 0x41, 0x2A, 0x0B);
        using var input = new MemoryStream([0xAA, 0xBB, .. bytes]);
        input.Position = 2;
        using var stream = new ChunkedReadStream(input, chunkSize);

        // Act
        var module = WasmModule.Decode(stream);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.InputLength).IsEqualTo((long)bytes.Length);
            await Assert.That(module.Functions.Length).IsEqualTo(1);
            await Assert.That(module.FunctionCodes.Length).IsEqualTo(0);
            await Assert.That(input.Position).IsEqualTo(input.Length);
            await Assert.That(stream.CanRead).IsTrue();
            await Assert.That(input.CanRead).IsTrue();
        }
    }

    [Test]
    public async Task 読み取りバッファより大きなcustomがある_拡張後も後続の定義を保持する()
    {
        // Arrange
        var constant = ConstantModuleBinary.Create(0x7F, 0x41, 0x2A, 0x0B);
        var data = new byte[20_000];
        Array.Fill(data, (byte)0xFF);
        // payload長20001はA1 9C 01。名前は空、その後に任意バイトを置く。
        byte[] bytes = [.. constant[..8], 0, 0xA1, 0x9C, 0x01, 0, .. data, .. constant[8..]];
        using var input = new ChunkedReadStream(new MemoryStream(bytes), 3000);

        // Act
        var module = WasmModule.Decode(input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.InputLength).IsEqualTo((long)bytes.Length);
            await Assert.That(module.Functions[0].Instructions[0].Immediate.AsI32()).IsEqualTo(42);
            await Assert.That(input.CanRead).IsTrue();
        }
    }

    [Test]
    public async Task 読み取り不可のストリーム_破損と異なる引数例外になる()
    {
        // Arrange
        var stream = new MemoryStream();
        stream.Dispose();

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(stream))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("stream");
    }

    [Test]
    public async Task ストリームでIO例外が発生する_元の例外の型と実体を維持する()
    {
        // Arrange
        var expected = new IOException("入力元で読み取りに失敗しました。");
        using var stream = new ThrowingReadStream(expected);

        // Act & Assert
        var actual = await Assert
            .That(() => WasmModule.Decode(stream))
            .ThrowsExactly<IOException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task 現在位置以降の入力が破損している_相対位置を通知して入力を閉じない()
    {
        // Arrange
        using var input = new MemoryStream(Convert.FromHexString("AABB0062736D01000000"));
        input.Position = 2;
        using var stream = new ChunkedReadStream(input);

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(stream))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Decode, 1));
            await Assert.That(input.Position).IsEqualTo(input.Length);
            await Assert.That(input.CanRead).IsTrue();
        }
    }

    [Test]
    [Arguments(false, "6AFF05", "i32.add")]
    [Arguments(true, "6AFF05", "i32.add")]
    [Arguments(false, "FC8000", "i32.trunc_sat_f32_s")]
    [Arguments(true, "FC8000", "i32.trunc_sat_f32_s")]
    [Arguments(false, "FD0C", "v128.const")]
    [Arguments(true, "FD0C", "v128.const")]
    public async Task 公開Decodeで未対応命令に遭遇する_デコーダーの診断を保持する(
        bool useStream,
        string instructions,
        string feature
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(instructions));
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 33, 0, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(33L);
            await Assert
                .That(exception.UnverifiedRanges[0].EndOffset)
                .IsEqualTo((long)bytes.Length);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
            await Assert
                .That(exception.UnverifiedRanges[1].EndOffset)
                .IsEqualTo((long)bytes.Length);
            await Assert.That(stream.CanRead).IsTrue();
        }
    }
}
