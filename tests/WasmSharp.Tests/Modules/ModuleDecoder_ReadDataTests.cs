using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleDecoder_ReadDataTests
{
    [Test]
    public async Task アクティブとパッシブの宣言がある_バイト列と初期化先と位置を独立して保持する()
    {
        // Arrange
        var bytes = Convert.FromHexString("03800041040B02AABB0101CC8200FFFFFFFF0F23030B00");
        var reader = new ModuleBinaryReader(bytes, 40, 11);

        // Act
        var segments = ModuleDecoder.ReadData(ref reader, 100);
        var remaining = reader.Remaining;
        Array.Clear(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(3);
            await Assert.That(Convert.ToHexString(segments[0].Bytes.AsSpan())).IsEqualTo("AABB");
            await Assert.That(segments[0].ByteOffset).IsEqualTo(41L);
            await Assert.That(segments[0].ActiveTarget!.MemoryIndex).IsEqualTo(0u);
            await Assert.That(segments[0].ActiveTarget!.OffsetExpression.Length).IsEqualTo(2);
            await Assert
                .That(segments[0].ActiveTarget!.OffsetExpression[0].Immediate.AsI32())
                .IsEqualTo(4);
            await Assert
                .That(segments[0].ActiveTarget!.OffsetExpression[0].ByteOffset)
                .IsEqualTo(43L);
            await Assert
                .That(segments[0].ActiveTarget!.OffsetExpression[1].ByteOffset)
                .IsEqualTo(45L);
            await Assert.That(Convert.ToHexString(segments[1].Bytes.AsSpan())).IsEqualTo("CC");
            await Assert.That(segments[1].ActiveTarget).IsNull();
            await Assert.That(segments[1].ByteOffset).IsEqualTo(49L);
            await Assert.That(segments[2].Bytes.IsDefault).IsFalse();
            await Assert.That(segments[2].Bytes.IsEmpty).IsTrue();
            await Assert.That(segments[2].ByteOffset).IsEqualTo(52L);
            await Assert.That(segments[2].ActiveTarget!.MemoryIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(segments[2].ActiveTarget!.OffsetExpression[0].Index).IsEqualTo(3u);
            await Assert
                .That(segments[2].ActiveTarget!.OffsetExpression[0].ByteOffset)
                .IsEqualTo(59L);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    public async Task データセグメント数が0である_空の定義を返す()
    {
        // Arrange
        var reader = new ModuleBinaryReader([0], 40, 11);

        // Act
        var segments = ModuleDecoder.ReadData(ref reader, 100);
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(0);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("03", 41L)]
    [Arguments("8300", 42L)]
    [Arguments("FFFFFFFF0F", 45L)]
    public async Task フラグが定義された値ではない_最終バイト位置で構文違反を報告する(
        string flags,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("01" + flags);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 11);
                ModuleDecoder.ReadData(ref reader, 100);
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "malformed data segment kind",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, 11));
        }
    }
}
