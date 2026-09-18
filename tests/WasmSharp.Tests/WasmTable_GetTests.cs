namespace WasmSharp.Tests;

internal class WasmTable_GetTests
{
    [Test]
    [Arguments(0u, 0u)]
    [Arguments(1u, 1u)]
    [Arguments(1u, uint.MaxValue)]
    public async Task 範囲外を取得する_契約違反として拒否する(uint count, uint index)
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new WasmLimits(count));

        // Act & Assert
        await Assert.That(() => table.Get(index)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(table.Count).IsEqualTo(count);
    }
}
