namespace WasmSharp.Tests;

internal class WasmLimits_ConstructorTests
{
    [Test]
    [Arguments(1u, null)]
    [Arguments(2u, 4u)]
    [Arguments(uint.MaxValue, 0u)]
    public async Task Limitsを記述する_不正な大小関係も保持して別の記述へコピーできる(
        uint minimum,
        uint? maximum
    )
    {
        // Arrange
        var limits = new WasmLimits(minimum, maximum);

        // Act
        var changed = limits with
        {
            Minimum = 0,
            Maximum = 10,
        };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(limits.Minimum).IsEqualTo(minimum);
            await Assert.That(limits.Maximum).IsEqualTo(maximum);
            await Assert.That(changed.Minimum).IsEqualTo(0u);
            await Assert.That(changed.Maximum).IsEqualTo(10u);
        }
    }
}
