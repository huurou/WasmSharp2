namespace WasmSharp.Tests;

internal class WasmMemory_TryCopyTests
{
    [Test]
    public async Task コピー元とコピー先が別々のページ境界をまたぐ_指定範囲だけコピーする()
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(3));
        memory.Write(65534, [1, 2, 3, 4, 5, 6, 7, 8]);
        var source = new byte[8];
        var destination = new byte[10];

        // Act
        var success = memory.TryCopy(131069, 65534, 8);
        memory.Read(65534, source);
        memory.Read(131068, destination);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(source.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })).IsTrue();
            await Assert
                .That(destination.SequenceEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 0 }))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(100UL)]
    [Arguments(65526UL)]
    [Arguments(65528UL)]
    [Arguments(65532UL)]
    public async Task コピー先がコピー元より後ろで重なる_コピー前の内容を転送する(ulong offset)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(2));
        memory.Write(offset, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        var actual = new byte[10];

        // Act
        var success = memory.TryCopy(offset + 2, offset, 8);
        memory.Read(offset, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert
                .That(actual.SequenceEqual(new byte[] { 1, 2, 1, 2, 3, 4, 5, 6, 7, 8 }))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(100UL)]
    [Arguments(65532UL)]
    public async Task コピー先がコピー元より前で重なる_コピー前の内容を転送する(ulong offset)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(2));
        memory.Write(offset, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        var actual = new byte[10];

        // Act
        var success = memory.TryCopy(offset, offset + 2, 8);
        memory.Read(offset, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert
                .That(actual.SequenceEqual(new byte[] { 3, 4, 5, 6, 7, 8, 9, 10, 9, 10 }))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(65534UL, 10UL)]
    [Arguments(10UL, 65534UL)]
    [Arguments(65534UL, 65534UL)]
    public async Task コピー元またはコピー先が末尾まである_全範囲をコピーする(
        ulong destination,
        ulong source
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));
        memory.Write(source, [21, 42]);
        var actual = new byte[2];

        // Act
        var success = memory.TryCopy(destination, source, 2);
        memory.Read(destination, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(actual.SequenceEqual(new byte[] { 21, 42 })).IsTrue();
        }
    }

    [Test]
    [Arguments(0u, 0UL, 0UL)]
    [Arguments(1u, 65536UL, 0UL)]
    [Arguments(1u, 0UL, 65536UL)]
    [Arguments(1u, 65536UL, 65536UL)]
    public async Task 長さがゼロで各開始位置が領域内または末尾にある_成功する(
        uint pages,
        ulong destination,
        ulong source
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(pages));

        // Act
        var success = memory.TryCopy(destination, source, 0);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(65535UL, 0UL, 2UL)]
    [Arguments(0UL, 65535UL, 2UL)]
    [Arguments(65537UL, 0UL, 0UL)]
    [Arguments(0UL, 65537UL, 0UL)]
    [Arguments(1UL, 0UL, ulong.MaxValue)]
    [Arguments(0UL, ulong.MaxValue, 2UL)]
    public async Task いずれかの範囲が領域外にある_内容を一部も変更せずfalseを返す(
        ulong destination,
        ulong source,
        ulong length
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));
        var expected = new byte[65536];
        Array.Fill(expected, (byte)42);
        expected[0] = 1;
        expected[1] = 2;
        memory.Write(0, expected);
        var actual = new byte[expected.Length];

        // Act
        var success = memory.TryCopy(destination, source, length);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        }
    }
}
