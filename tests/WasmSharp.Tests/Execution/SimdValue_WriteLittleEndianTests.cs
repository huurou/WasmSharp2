using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class SimdValue_WriteLittleEndianTests
{
    [Test]
    public async Task 上下64ビットを書き出す_低位からLittleEndianの16バイトを返す()
    {
        // Arrange
        var value = SimdValue.FromValue(
            WasmValue.FromV128(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL)
        );
        var bytes = new byte[16];
        byte[] expected =
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
        value.WriteLittleEndian(bytes);

        // Assert
        await Assert.That(bytes.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    [Arguments(0UL, 0UL)]
    [Arguments(ulong.MaxValue, ulong.MaxValue)]
    [Arguments(0x800000007F812345UL, 0x7FF0000000000001UL)]
    public async Task バイト列へ変換して読み直す_全128ビットが一致する(ulong low64, ulong high64)
    {
        // Arrange
        var value = SimdValue.FromValue(WasmValue.FromV128(low64, high64));
        var bytes = new byte[16];

        // Act
        value.WriteLittleEndian(bytes);
        var actual = SimdValue.FromLittleEndian(bytes).ToValue().AsV128();

        // Assert
        await Assert.That(actual).IsEqualTo((low64, high64));
    }
}
