using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4TruncSatF32x4UTests
{
    [Test]
    [Arguments(
        0xBFF333333FF33333UL,
        0xBF0000003F000000UL,
        0x0000000000000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x8000000100000001UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x4F8000004F7FFFFFUL,
        0xBF8000004F000000UL,
        0xFFFFFFFFFFFFFF00UL,
        0x0000000080000000UL
    )]
    [Arguments(
        0x7F7FFFFF4F800001UL,
        0x80000001FF7FFFFFUL,
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xFF8000007F800000UL,
        0x41480000412C0000UL,
        0x00000000FFFFFFFFUL,
        0x0000000C0000000AUL
    )]
    [Arguments(
        0xFFC543217FC12345UL,
        0xFF8543217F812345UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    public async Task 境界値と特殊値を含むlane_符号なしi32へ飽和変換する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4TruncSatF32x4U, operands);

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
