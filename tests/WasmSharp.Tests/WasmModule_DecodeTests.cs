using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_DecodeTests
{
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
    [Arguments(false)]
    [Arguments(true)]
    public async Task 公開Decodeで未対応命令に遭遇する_デコーダーの診断を保持する(bool useStream)
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, 0x6A, 0x0B);
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        var exception = await Assert
            .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("i32.add");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 33, 0, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert.That(stream.CanRead).IsTrue();
        }
    }
}
