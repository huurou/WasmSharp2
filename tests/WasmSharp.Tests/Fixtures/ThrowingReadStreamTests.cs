namespace WasmSharp.Tests.Fixtures;

internal class ThrowingReadStream_ReadTests
{
    [Test]
    public async Task 配列へ読み取る_指定したIO例外の実体を投げる()
    {
        // Arrange
        var expected = new IOException("読み取りに失敗しました。");
        using var stream = new ThrowingReadStream(expected);

        // Act & Assert
        var actual = await Assert
            .That(() => stream.Read(new byte[1], 0, 1))
            .ThrowsExactly<IOException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task Spanへ読み取る_指定したIO例外の実体を投げる()
    {
        // Arrange
        var expected = new IOException("読み取りに失敗しました。");
        using var stream = new ThrowingReadStream(expected);

        // Act & Assert
        var actual = await Assert
            .That(() => stream.Read(new byte[1].AsSpan()))
            .ThrowsExactly<IOException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
    }
}
