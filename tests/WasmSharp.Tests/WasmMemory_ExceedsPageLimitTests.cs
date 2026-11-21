namespace WasmSharp.Tests;

internal class WasmMemory_ExceedsPageLimitTests
{
    [Test]
    [Arguments(65535UL, null, false)]
    [Arguments(65536UL, null, false)]
    [Arguments(65537UL, null, true)]
    [Arguments(1UL, 2u, false)]
    [Arguments(2UL, 2u, false)]
    [Arguments(3UL, 2u, true)]
    public async Task 仕様上限と宣言上限の境界を判定する_いずれかを超える場合だけtrueを返す(
        ulong pageCount,
        uint? maximumPages,
        bool expected
    )
    {
        // Arrange
        var type = new WasmMemoryType(WasmValueKind.I32, 0, maximumPages);

        // Act
        var actual = WasmMemory.ExceedsPageLimit(pageCount, type);

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }
}
