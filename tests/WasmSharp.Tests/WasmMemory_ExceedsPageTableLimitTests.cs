namespace WasmSharp.Tests;

internal class WasmMemory_ExceedsPageTableLimitTests
{
    [Test]
    [Arguments(0UL, false)]
    [Arguments(1UL, false)]
    [Arguments(2UL, true)]
    [Arguments(ulong.MaxValue, true)]
    public async Task ページ表の残り容量が1ページである_2ページ以上の追加だけが保持上限を超える(
        ulong additionalPages,
        bool expected
    )
    {
        // Arrange
        var currentPages = (uint)Array.MaxLength - 1;

        // Act
        var exceedsLimit = WasmMemory.ExceedsPageTableLimit(currentPages, additionalPages);

        // Assert
        await Assert.That(exceedsLimit).IsEqualTo(expected);
    }
}
