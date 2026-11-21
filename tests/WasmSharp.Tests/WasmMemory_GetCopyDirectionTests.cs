namespace WasmSharp.Tests;

internal class WasmMemory_GetCopyDirectionTests
{
    [Test]
    [Arguments(99UL, MemoryCopyDirection.Forward)]
    [Arguments(100UL, MemoryCopyDirection.None)]
    [Arguments(101UL, MemoryCopyDirection.Backward)]
    public async Task 同じメモリでコピー元が100のとき_コピー先の位置に応じて方向または省略を返す(
        ulong destination,
        MemoryCopyDirection expected
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(1));

        // Act
        var direction = memory.GetCopyDirection(memory, destination, source: 100);

        // Assert
        await Assert.That(direction).IsEqualTo(expected);
    }

    [Test]
    public async Task 異なるメモリでコピー元とコピー先が同じ位置にある_先頭からコピーする方向を返す()
    {
        // Arrange
        var sourceMemory = new WasmMemory(new WasmLimits(1));
        var destinationMemory = new WasmMemory(new WasmLimits(1));

        // Act
        var direction = destinationMemory.GetCopyDirection(sourceMemory, destination: 100, source: 100);

        // Assert
        await Assert.That(direction).IsEqualTo(MemoryCopyDirection.Forward);
    }
}
