using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class SimdValue_GetLaneTests
{
    [Test]
    [Arguments(8, 0, 0xEFUL)]
    [Arguments(8, 7, 0x01UL)]
    [Arguments(8, 8, 0x10UL)]
    [Arguments(8, 15, 0xFEUL)]
    [Arguments(16, 0, 0xCDEFUL)]
    [Arguments(16, 3, 0x0123UL)]
    [Arguments(16, 4, 0x3210UL)]
    [Arguments(16, 7, 0xFEDCUL)]
    [Arguments(32, 0, 0x89ABCDEFUL)]
    [Arguments(32, 1, 0x01234567UL)]
    [Arguments(32, 2, 0x76543210UL)]
    [Arguments(32, 3, 0xFEDCBA98UL)]
    [Arguments(64, 0, 0x0123456789ABCDEFUL)]
    [Arguments(64, 1, 0xFEDCBA9876543210UL)]
    public async Task Lane幅と位置を指定する_低位から対応するビット列を取得する(
        int bitWidth,
        int laneIndex,
        ulong expected
    )
    {
        // Arrange
        var value = SimdValue.FromValue(
            WasmValue.FromV128(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL)
        );

        // Act
        var actual = value.GetLane(bitWidth, laneIndex);

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }
}
