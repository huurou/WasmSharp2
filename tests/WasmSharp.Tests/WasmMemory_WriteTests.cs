namespace WasmSharp.Tests;

internal class WasmMemory_WriteTests
{
    [Test]
    public async Task ページ境界を跨いで書き込む_全バイトをコピーし元バッファから独立する()
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(3, null));
        var source = Enumerable.Range(0, 65538).Select(x => (byte)(x % 251 + 1)).ToArray();
        var expected = source.ToArray();
        var actual = new byte[source.Length + 2];

        // Act
        memory.Write(65535, source);
        Array.Clear(source);
        memory.Read(65534, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.AsSpan()[1..^1].SequenceEqual(expected)).IsTrue();
            await Assert.That(actual[0]).IsEqualTo((byte)0);
            await Assert.That(actual[^1]).IsEqualTo((byte)0);
        }
    }

    [Test]
    [Arguments(65535UL, 2)]
    [Arguments(65536UL, 1)]
    [Arguments(65537UL, 0)]
    [Arguments(ulong.MaxValue, 2)]
    public async Task 範囲外へ書き込む_部分変更を残さず拒否する(ulong offset, int length)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1, null));
        memory.Write(65535, [42]);
        var source = new byte[length];
        Array.Fill(source, (byte)99);

        // Act & Assert
        await Assert
            .That(() => memory.Write(offset, source))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        var actual = new byte[1];
        memory.Read(65535, actual);
        await Assert.That(actual[0]).IsEqualTo((byte)42);
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    public async Task 末尾へ長さゼロで書き込む_サイズを変えずに成功する(uint pages)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(pages, null));

        // Act
        memory.Write(memory.ByteLength, []);

        // Assert
        await Assert.That(memory.PageCount).IsEqualTo(pages);
    }
}
