using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class SimdValue_FromValueTests
{
    [Test]
    [Arguments(0UL, 0UL)]
    [Arguments(ulong.MaxValue, ulong.MaxValue)]
    [Arguments(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL)]
    [Arguments(0x800000007F812345UL, 0x7FF0000000000001UL)]
    public async Task V128を往復する_種類と上下128ビットを保持する(ulong low64, ulong high64)
    {
        // Arrange
        var input = WasmValue.FromV128(low64, high64);

        // Act
        var actual = SimdValue.FromValue(input).ToValue();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(actual.AsV128()).IsEqualTo((low64, high64));
        }
    }
}
