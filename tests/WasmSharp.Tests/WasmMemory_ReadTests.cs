namespace WasmSharp.Tests;

internal class WasmMemory_ReadTests
{
    [Test]
    public async Task 読み出し後にmemoryを更新する_取得済みコピーは変化しない()
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(2, null));
        memory.Write(65535, [1, 2, 3]);
        var copy = new byte[3];
        var current = new byte[3];

        // Act
        memory.Read(65535, copy);
        memory.Write(65535, [4, 5, 6]);
        memory.Read(65535, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(copy.SequenceEqual(new byte[] { 1, 2, 3 })).IsTrue();
            await Assert.That(current.SequenceEqual(new byte[] { 4, 5, 6 })).IsTrue();
        }
    }

    [Test]
    [Arguments(65535UL, 2)]
    [Arguments(65536UL, 1)]
    [Arguments(65537UL, 0)]
    [Arguments(ulong.MaxValue, 2)]
    public async Task 範囲外から読み出す_転送先を一部も変更せず拒否する(ulong offset, int length)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1, null));
        var destination = new byte[length];
        Array.Fill(destination, (byte)42);

        // Act & Assert
        await Assert
            .That(() => memory.Read(offset, destination))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(destination.All(x => x == 42)).IsTrue();
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    public async Task 末尾から長さゼロで読み出す_成功する(uint pages)
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(pages, null));

        // Act & Assert
        await Assert.That(() => memory.Read(memory.ByteLength, [])).ThrowsNothing();
    }
}
