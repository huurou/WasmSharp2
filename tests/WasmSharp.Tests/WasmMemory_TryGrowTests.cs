namespace WasmSharp.Tests;

internal class WasmMemory_TryGrowTests
{
    [Test]
    public async Task 増大する_既存バイトを保持し追加ページをゼロ初期化する()
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1, 3));
        var alias = memory;
        var original = Enumerable.Range(0, 65536).Select(x => (byte)(x % 251 + 1)).ToArray();
        memory.Write(0, original);
        var copy = new byte[original.Length];
        memory.Read(0, copy);
        var actual = new byte[196608];

        // Act
        var success = alias.TryGrow(2, out var previous);
        memory.Read(0, actual);
        memory.Write(65535, [99, 100]);
        var boundary = new byte[2];
        memory.Read(65535, boundary);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(previous).IsEqualTo(1u);
            await Assert.That(memory.PageCount).IsEqualTo(3u);
            await Assert.That(memory.MaximumPages).IsEqualTo(3u);
            await Assert.That(memory.ByteLength).IsEqualTo(196608UL);
            await Assert.That(actual.AsSpan()[..65536].SequenceEqual(original)).IsTrue();
            await Assert.That(actual[65536..].All(x => x == 0)).IsTrue();
            await Assert.That(copy.SequenceEqual(original)).IsTrue();
#pragma warning disable IDE0230 // UTF-8 文字列リテラルを使用する
            await Assert.That(boundary.SequenceEqual(new byte[] { 99, 100 })).IsTrue();
#pragma warning restore IDE0230 // UTF-8 文字列リテラルを使用する
        }
    }

    [Test]
    public async Task 空のmemoryを増大する_最初のページをゼロ初期化する()
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(0));
        var bytes = new byte[65536];
        Array.Fill(bytes, (byte)255);

        // Act
        var success = memory.TryGrow(1, out var previous);
        memory.Read(0, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(previous).IsEqualTo(0u);
            await Assert.That(memory.PageCount).IsEqualTo(1u);
            await Assert.That(bytes.All(x => x == 0)).IsTrue();
        }
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    public async Task 最大値で増大量ゼロを指定する_現在サイズを返して成功する(uint pages)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(pages, pages));

        // Act
        var success = memory.TryGrow(0, out var previous);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(previous).IsEqualTo(pages);
            await Assert.That(memory.PageCount).IsEqualTo(pages);
        }
    }

    [Test]
    [Arguments(2u, 2u)]
    [Arguments(null, 65536u)]
    [Arguments(null, uint.MaxValue)]
    public async Task 宣言または仕様上限を超える_現在サイズを返して内容を保ちfalseになる(
        uint? maximum,
        uint delta
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1, maximum));
        memory.Write(65535, [42]);
        var actual = new byte[1];

        // Act
        var success = memory.TryGrow(delta, out var previous);
        memory.Read(65535, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(previous).IsEqualTo(1u);
            await Assert.That(memory.PageCount).IsEqualTo(1u);
            await Assert.That(memory.ByteLength).IsEqualTo(65536UL);
            await Assert.That(actual[0]).IsEqualTo((byte)42);
        }
    }
}
