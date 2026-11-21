namespace WasmSharp.Tests;

internal class WasmMemory_MaximumPagesTests
{
    [Test]
    [Arguments(null)]
    [Arguments(uint.MaxValue)]
    public async Task アドレス型がi64で上限を省略するかuintの範囲で指定する_宣言どおりの最大ページ数を返す(uint? maximum)
    {
        // Arrange
        var memory = WasmMemory.Create(new WasmMemoryType(WasmValueKind.I64, 0, maximum));

        // Act
        var actual = memory.MaximumPages;

        // Assert
        await Assert.That(actual).IsEqualTo(maximum);
    }

    [Test]
    public async Task アドレス型がi64で最大ページ数がuintの最大値を1だけ超える_取得時に例外を返す()
    {
        // Arrange
        var memory = WasmMemory.Create(new WasmMemoryType(WasmValueKind.I64, 0, (ulong)uint.MaxValue + 1));

        // Act & Assert
        var exception = await Assert.That(() => memory.MaximumPages).ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains("Type.MaximumPages");
            await Assert.That(memory.Type.MaximumPages).IsEqualTo((ulong)uint.MaxValue + 1);
        }
    }
}
