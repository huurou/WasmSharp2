namespace WasmSharp.Tests;

internal class WasmTable_IndexerTests
{
    [Test]
    public async Task 上位32ビットを持つ位置を取得する_先頭の参照を返さず範囲外として拒否する()
    {
        // Arrange
        var table = WasmTable.Create(new WasmTableType(WasmValueKind.FuncRef, WasmValueKind.I64, 1));

        // Act & Assert
        await Assert.That(() => table[1UL << 32]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task 上位32ビットを持つ位置に設定する_先頭要素を書き換えず範囲外として拒否する()
    {
        // Arrange
        var table = WasmTable.Create(new WasmTableType(WasmValueKind.ExternRef, WasmValueKind.I64, 1));
        var reference = new object();
        table[0UL] = WasmValue.FromExternRef(reference);

        using (Assert.Multiple())
        {
            // Act & Assert
            await Assert
                .That(() => table[1UL << 32] = WasmValue.FromExternRef(null))
                .ThrowsExactly<ArgumentOutOfRangeException>();
            await Assert.That(table[0UL].AsExternRef()).IsSameReferenceAs(reference);
        }
    }
}
