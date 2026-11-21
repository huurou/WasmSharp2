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
        var exception = await Assert.That(() => table.Get(index)).ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("index");
            await Assert.That(exception.Message).StartsWith("tableの範囲外です。");
            await Assert.That(table.Count).IsEqualTo(count);
        }
    }

    [Test]
    public async Task 上位32ビットを持つ位置を取得する_先頭の参照を返さず範囲外として拒否する()
    {
        // Arrange
        var table = WasmTable.Create(new WasmTableType(WasmValueKind.FuncRef, WasmValueKind.I64, 1));

        // Act & Assert
        await Assert.That(() => table.Get(1UL << 32)).ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
