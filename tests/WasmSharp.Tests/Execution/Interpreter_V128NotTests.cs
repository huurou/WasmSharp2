using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128NotTests
{
    [Test]
    [Arguments(0UL, 0UL, 0xFFFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL, 0UL, 0UL)]
    [Arguments(
        0x0123456789ABCDEFUL,
        0xFEDCBA9876543210UL,
        0xFEDCBA9876543210UL,
        0x0123456789ABCDEFUL
    )]
    public async Task ゼロと全bit1と混在bit_上下64bitすべてを反転する(
        ulong operandLow64,
        ulong operandHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(operandLow64, operandHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.V128Not, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(actual.Value.AsV128()).IsEqualTo((expectedLow64, expectedHigh64));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
