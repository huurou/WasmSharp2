using TUnit.Assertions.Enums;

namespace WasmSharp.Tests.Fixtures;

public class ChunkedReadStream_ReadTests
{
    [Test]
    public async Task 配列へ繰り返し読み取る_指定位置へ短く読みEOFで0を返す()
    {
        // Arrange
        using var stream = new ChunkedReadStream(new MemoryStream([1, 2, 3, 4, 5]), 2);
        byte[] buffer = [9, 9, 9, 9, 9, 9, 9];

        // Act
        var first = stream.Read(buffer, 1, 5);
        var second = stream.Read(buffer, 3, 3);
        var third = stream.Read(buffer, 5, 1);
        var end = stream.Read(buffer, 6, 1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That([first, second, third, end])
                .IsEquivalentTo([2, 2, 1, 0], CollectionOrdering.Matching);
            await Assert
                .That(buffer)
                .IsEquivalentTo(new byte[] { 9, 1, 2, 3, 4, 5, 9 }, CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task 現在位置のある入力をSpanへ読み取る_先頭へ戻さず短く読む()
    {
        // Arrange
        using var input = new MemoryStream([9, 9, 1, 2, 3]);
        input.Position = 2;
        using var stream = new ChunkedReadStream(input, 2);
        byte[] buffer = [0, 0, 0];

        // Act
        var count = stream.Read(buffer.AsSpan());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(count).IsEqualTo(2);
            await Assert
                .That(buffer)
                .IsEquivalentTo(new byte[] { 1, 2, 0 }, CollectionOrdering.Matching);
            await Assert.That(input.Position).IsEqualTo(4);
        }
    }

    [Test]
    public async Task 空の領域へ読み取る_入力を消費しない()
    {
        // Arrange
        using var stream = new ChunkedReadStream(new MemoryStream([42]));

        // Act
        var count = stream.Read([]);
        var next = stream.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(count).IsEqualTo(0);
            await Assert.That(next).IsEqualTo(42);
        }
    }

    [Test]
    public async Task 内側が読み取り不可_読み取り可否と例外を維持する()
    {
        // Arrange
        var input = new MemoryStream([1]);
        input.Dispose();
        using var stream = new ChunkedReadStream(input);

        // Assert
        await Assert.That(stream.CanRead).IsFalse();

        // Act & Assert
        await Assert
            .That(() => stream.Read(new byte[1], 0, 1))
            .ThrowsExactly<ObjectDisposedException>();
    }
}

public class ChunkedReadStream_SeekTests
{
    [Test]
    public async Task Seek可能な入力を包む_非seekとして位置と長さの照会を拒否する()
    {
        // Arrange
        using var stream = new ChunkedReadStream(new MemoryStream([1]));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stream.CanSeek).IsFalse();
            await Assert.That(stream.CanRead).IsTrue();
            await Assert.That(stream.CanWrite).IsFalse();
        }

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(() => stream.Seek(0, SeekOrigin.Begin))
                .ThrowsExactly<NotSupportedException>();
            await Assert.That(() => stream.Position).ThrowsExactly<NotSupportedException>();
            await Assert.That(() => stream.Position = 0).ThrowsExactly<NotSupportedException>();
            await Assert.That(() => stream.Length).ThrowsExactly<NotSupportedException>();
        }
    }
}

public class ChunkedReadStream_DisposeTests
{
    [Test]
    public async Task 破棄する_内側のストリームも閉じる()
    {
        // Arrange
        using var input = new MemoryStream([1]);
        var stream = new ChunkedReadStream(input);

        // Act
        stream.Dispose();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(input.CanRead).IsFalse();
            await Assert.That(stream.CanRead).IsFalse();
        }
    }
}
