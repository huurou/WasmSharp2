using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class SimdValue_WithLaneTests
{
    [Test]
    [Arguments(8, 0, 0x0123456789ABCD78UL, 0xFEDCBA9876543210UL)]
    [Arguments(8, 7, 0x7823456789ABCDEFUL, 0xFEDCBA9876543210UL)]
    [Arguments(8, 8, 0x0123456789ABCDEFUL, 0xFEDCBA9876543278UL)]
    [Arguments(8, 15, 0x0123456789ABCDEFUL, 0x78DCBA9876543210UL)]
    [Arguments(16, 0, 0x0123456789AB6078UL, 0xFEDCBA9876543210UL)]
    [Arguments(16, 3, 0x6078456789ABCDEFUL, 0xFEDCBA9876543210UL)]
    [Arguments(16, 4, 0x0123456789ABCDEFUL, 0xFEDCBA9876546078UL)]
    [Arguments(16, 7, 0x0123456789ABCDEFUL, 0x6078BA9876543210UL)]
    [Arguments(32, 0, 0x012345674E5F6078UL, 0xFEDCBA9876543210UL)]
    [Arguments(32, 1, 0x4E5F607889ABCDEFUL, 0xFEDCBA9876543210UL)]
    [Arguments(32, 2, 0x0123456789ABCDEFUL, 0xFEDCBA984E5F6078UL)]
    [Arguments(32, 3, 0x0123456789ABCDEFUL, 0x4E5F607876543210UL)]
    [Arguments(64, 0, 0xA1B2C3D44E5F6078UL, 0xFEDCBA9876543210UL)]
    [Arguments(64, 1, 0x0123456789ABCDEFUL, 0xA1B2C3D44E5F6078UL)]
    public async Task Laneを置換する_幅を超えた入力を切り詰め対象以外のビットを保持する(
        int bitWidth,
        int laneIndex,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        var original = WasmValue.FromV128(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL);
        var value = SimdValue.FromValue(original);

        // Act
        var actual = value.WithLane(bitWidth, laneIndex, 0xA1B2C3D44E5F6078UL).ToValue();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.AsV128()).IsEqualTo((expectedLow64, expectedHigh64));
            await Assert.That(value.ToValue().AsV128()).IsEqualTo(original.AsV128());
        }
    }
}
