using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8ExtAddPairwiseI8x16UTests
{
    [Test]
    [Arguments(
        0x00FF7F807F7F8080UL,
        0x7F7EFF8002010100UL,
        0x00FF00FF00FE0100UL,
        0x00FD017F00030001UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8ExtAddPairwiseI8x16U, operands);

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
