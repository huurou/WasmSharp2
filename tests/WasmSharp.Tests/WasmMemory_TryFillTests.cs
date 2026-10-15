namespace WasmSharp.Tests;

internal class WasmMemory_TryFillTests
{
    [Test]
    [Arguments((byte)0)]
    [Arguments((byte)128)]
    [Arguments((byte)255)]
    public async Task 複数のページ境界をまたぐ_指定範囲の各バイトだけを指定値で埋める(byte value)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(3));
        var initial = new byte[65542];
        Array.Fill(initial, (byte)42);
        initial[0] = 17;
        initial[^1] = 18;
        memory.Write(65533, initial);
        var actual = new byte[initial.Length];

        // Act
        var success = memory.TryFill(65534, value, 65540);
        memory.Read(65533, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(actual[0]).IsEqualTo((byte)17);
            await Assert.That(actual.Skip(1).Take(65540).All(x => x == value)).IsTrue();
            await Assert.That(actual[^1]).IsEqualTo((byte)18);
        }
    }

    [Test]
    public async Task 指定範囲が末尾まである_末尾のバイトも指定値で埋める()
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));
        memory.Write(65533, [1, 2, 3]);
        var actual = new byte[3];

        // Act
        var success = memory.TryFill(65534, 171, 2);
        memory.Read(65533, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(actual.SequenceEqual(new byte[] { 1, 171, 171 })).IsTrue();
        }
    }

    [Test]
    [Arguments(0u, 0UL)]
    [Arguments(1u, 0UL)]
    [Arguments(1u, 65536UL)]
    public async Task 長さがゼロで開始位置が領域内または末尾にある_成功する(
        uint pages,
        ulong destination
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(pages));

        // Act
        var success = memory.TryFill(destination, 255, 0);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(65535UL, 2UL)]
    [Arguments(65537UL, 0UL)]
    [Arguments(1UL, ulong.MaxValue)]
    [Arguments(ulong.MaxValue, 2UL)]
    public async Task 指定範囲が領域外にある_内容を一部も変更せずfalseを返す(
        ulong destination,
        ulong length
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));
        var expected = new byte[65536];
        Array.Fill(expected, (byte)42);
        memory.Write(0, expected);
        var actual = new byte[expected.Length];

        // Act
        var success = memory.TryFill(destination, 99, length);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        }
    }
}
