namespace WasmSharp.Tests;

internal class WasmTable_ExceedsStorageLimitTests
{
    [Test]
    [Arguments(-1, false)]
    [Arguments(0, false)]
    [Arguments(1, true)]
    public async Task 配列保持上限の境界を判定する_上限を超える場合だけtrueを返す(
        int offset,
        bool expected
    )
    {
        // Arrange
        var count = (ulong)((long)Array.MaxLength + offset);

        // Act
        var actual = WasmTable.ExceedsStorageLimit(count);

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }
}
