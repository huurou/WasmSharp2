using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4TruncSatF32x4STests
{
    [Test]
    [Arguments(
        0xBFF333333FF33333UL,
        0xBF0000003F000000UL,
        0xFFFFFFFF00000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x8000000100000001UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x4F0000004EFFFFFFUL,
        0xCF000001CF000000UL,
        0x7FFFFFFF7FFFFF80UL,
        0x8000000080000000UL
    )]
    [Arguments(
        0xCF000000CEFFFFFFUL,
        0x7F7FFFFF4F000001UL,
        0x8000000080000080UL,
        0x7FFFFFFF7FFFFFFFUL
    )]
    [Arguments(
        0xFF8000007F800000UL,
        0xFF7FFFFF7F7FFFFFUL,
        0x800000007FFFFFFFUL,
        0x800000007FFFFFFFUL
    )]
    [Arguments(
        0xFFC543217FC12345UL,
        0xFF8543217F812345UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    public async Task 境界値と特殊値を含むlane_符号付きi32へ飽和変換する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4TruncSatF32x4S, operands);

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
