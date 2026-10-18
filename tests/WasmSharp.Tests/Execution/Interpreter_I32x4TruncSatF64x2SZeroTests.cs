using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4TruncSatF64x2SZeroTests
{
    [Test]
    [Arguments(
        0x3FFE666666666666UL,
        0xBFFE666666666666UL,
        0xFFFFFFFF00000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x3FE0000000000000UL,
        0xBFE0000000000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x0000000000000000UL,
        0x8000000000000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x0000000000000001UL,
        0x8000000000000001UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x41DFFFFFFFFFFFFFUL,
        0x41E0000000000000UL,
        0x7FFFFFFF7FFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xC1DFFFFFFFFFFFFFUL,
        0xC1E0000000000000UL,
        0x8000000080000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xC1E0000000000001UL,
        0xC1E0000000200000UL,
        0x8000000080000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x41DFFFFFFFC00000UL,
        0x41E0000000100000UL,
        0x7FFFFFFF7FFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF0000000000000UL,
        0xFFF0000000000000UL,
        0x800000007FFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FEFFFFFFFFFFFFFUL,
        0xFFEFFFFFFFFFFFFFUL,
        0x800000007FFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF8123456789ABCUL,
        0xFFF8FEDCBA987654UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF0123456789ABCUL,
        0xFFF0FEDCBA987654UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x4045600000000000UL,
        0xC05EF80000000000UL,
        0xFFFFFF850000002AUL,
        0x0000000000000000UL
    )]
    public async Task 境界値と特殊値を含むlane_符号付きi32へ飽和変換し上位laneをゼロにする(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4TruncSatF64x2SZero, operands);

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
