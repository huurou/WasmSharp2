namespace WasmSharp.Tests;

internal class WasmTable_MaximumElementsTests
{
    [Test]
    [Arguments(null)]
    [Arguments(uint.MaxValue)]
    public async Task 上限を省略するか32ビットに収まる値を指定する_64ビットテーブルでも宣言値を返す(uint? maximum)
    {
        // Arrange
        var table = WasmTable.Create(new WasmTableType(WasmValueKind.FuncRef, WasmValueKind.I64, 0, maximum));

        // Act
        var actual = table.MaximumElements;

        // Assert
        await Assert.That(actual).IsEqualTo(maximum);
    }

    [Test]
    public async Task 宣言上限が32ビットを超える_値を切り詰めず64ビットの取得方法を示す()
    {
        // Arrange
        var table = WasmTable.Create(new WasmTableType(WasmValueKind.FuncRef, WasmValueKind.I64, 0, 1UL << 32));

        // Act & Assert
        var exception = await Assert.That(() => table.MaximumElements).ThrowsExactly<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("Type.MaximumElements");
    }
}
