namespace WasmSharp.Tests;

internal class WasmMemory_ContainsRangeTests
{
    [Test]
    [Arguments(0u, 0UL, 0UL, true)]
    [Arguments(0u, 1UL, 0UL, false)]
    [Arguments(1u, 65535UL, 1UL, true)]
    [Arguments(1u, 65536UL, 0UL, true)]
    [Arguments(1u, 65537UL, 0UL, false)]
    [Arguments(1u, 65535UL, 2UL, false)]
    [Arguments(1u, 1UL, ulong.MaxValue, false)]
    [Arguments(1u, ulong.MaxValue, 1UL, false)]
    public async Task 指定範囲を判定する_全範囲がmemory内に収まる場合だけtrueを返す(
        uint pages,
        ulong offset,
        ulong length,
        bool expected
    )
    {
        // Arrange
        var memory = new WasmMemory(new WasmLimits(pages));

        // Act
        var actual = memory.ContainsRange(offset, length);

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }
}
