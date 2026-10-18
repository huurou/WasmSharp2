using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2ExtMulLowI32x4UTests
{
    [Test]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000000FFFFFFFFUL,
        0x8000000080000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        0x4000000000000000UL,
        0x3FFFFFFF80000000UL
    )]
    [Arguments(
        0x00000000FFFFFFFFUL,
        0x800000007FFFFFFFUL,
        0x7FFFFFFFFFFFFFFFUL,
        0x8000000080000000UL,
        0xFFFFFFFE00000001UL,
        0x0000000000000000UL
    )]
    public async Task 上下で異なる符号境界のlane_選択した半分を拡張幅で乗算する(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2ExtMulLowI32x4U, operands);

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
