using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class SimdValue_FromLittleEndianTests
{
    [Test]
    public async Task LittleEndianの16バイトを読み込む_先頭を低位にして上下64ビットを保持する()
    {
        // Arrange
        byte[] bytes =
        [
            0xEF,
            0xCD,
            0xAB,
            0x89,
            0x67,
            0x45,
            0x23,
            0x01,
            0x10,
            0x32,
            0x54,
            0x76,
            0x98,
            0xBA,
            0xDC,
            0xFE,
        ];

        // Act
        var actual = SimdValue.FromLittleEndian(bytes).ToValue().AsV128();

        // Assert
        await Assert.That(actual).IsEqualTo((0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL));
    }
}
