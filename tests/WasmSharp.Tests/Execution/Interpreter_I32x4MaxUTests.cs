using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4MaxUTests
{
    [Test]
    [Arguments(
        0x800000007FFFFFFFUL,
        0x00000000FFFFFFFFUL,
        0x7FFFFFFF80000000UL,
        0xFFFFFFFF00000000UL,
        0x8000000080000000UL,
        0xFFFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x7FFFFFFFFFFFFFFFUL,
        0x0000000200000001UL,
        0x7FFFFFFFFFFFFFFFUL,
        0x0000000100000002UL,
        0x7FFFFFFFFFFFFFFFUL,
        0x0000000200000002UL
    )]
    public async Task 符号境界と等値と左右大小を含む上下のlane_符号なしの最大値を選ぶ(
        ulong firstLow64,
        ulong firstHigh64,
        ulong secondLow64,
        ulong secondHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(firstLow64, firstHigh64),
            WasmValue.FromV128(secondLow64, secondHigh64),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4MaxU, operands);

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
