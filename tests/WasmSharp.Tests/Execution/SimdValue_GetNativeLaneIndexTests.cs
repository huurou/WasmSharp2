using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class SimdValue_GetNativeLaneIndexTests
{
    [Test]
    [Arguments(8, 0, 7)]
    [Arguments(8, 2, 5)]
    [Arguments(8, 8, 15)]
    [Arguments(8, 15, 8)]
    [Arguments(16, 1, 2)]
    [Arguments(16, 4, 7)]
    [Arguments(32, 0, 1)]
    [Arguments(32, 3, 2)]
    [Arguments(64, 0, 0)]
    [Arguments(64, 1, 1)]
    public async Task ホストのバイト順を指定する_上下それぞれの64ビット内で対応する要素番号を返す(
        int bitWidth,
        int laneIndex,
        int bigEndianIndex
    )
    {
        // Act
        var littleEndian = SimdValue.GetNativeLaneIndex(bitWidth, laneIndex, true);
        var bigEndian = SimdValue.GetNativeLaneIndex(bitWidth, laneIndex, false);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(littleEndian).IsEqualTo(laneIndex);
            await Assert.That(bigEndian).IsEqualTo(bigEndianIndex);
        }
    }
}
