using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4ExtAddPairwiseI16x8UTests
{
    [Test]
    [Arguments(
        0x7FFF7FFF80008000UL,
        0xFFFEFFFF7FFF8000UL,
        0x0000FFFE00010000UL,
        0x0001FFFD0000FFFFUL
    )]
    public async Task 上下で異なる符号境界のlane_隣接対を拡張して順に加算する(
        ulong firstLow64,
        ulong firstHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(firstLow64, firstHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4ExtAddPairwiseI16x8U, operands);

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
