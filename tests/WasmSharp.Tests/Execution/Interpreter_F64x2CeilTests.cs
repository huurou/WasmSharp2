using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2CeilTests
{
    [Test]
    [Arguments(
        0xBFE0000000000000UL,
        0x3FE0000000000000UL,
        0x8000000000000000UL,
        0x3FF0000000000000UL
    )]
    [Arguments(
        0xBFF8000000000000UL,
        0x3FF8000000000000UL,
        0xBFF0000000000000UL,
        0x4000000000000000UL
    )]
    [Arguments(
        0xC004000000000000UL,
        0x4004000000000000UL,
        0xC000000000000000UL,
        0x4008000000000000UL
    )]
    [Arguments(
        0x0000000000000001UL,
        0x8000000000000001UL,
        0x3FF0000000000000UL,
        0x8000000000000000UL
    )]
    [Arguments(
        0x0000000000000000UL,
        0x3FF4000000000000UL,
        0x0000000000000000UL,
        0x4000000000000000UL
    )]
    [Arguments(
        0x8000000000000000UL,
        0xBFF4000000000000UL,
        0x8000000000000000UL,
        0xBFF0000000000000UL
    )]
    [Arguments(
        0x7FF0000000000000UL,
        0xBFD0000000000000UL,
        0x7FF0000000000000UL,
        0x8000000000000000UL
    )]
    [Arguments(
        0xFFF0000000000000UL,
        0x3FD0000000000000UL,
        0xFFF0000000000000UL,
        0x3FF0000000000000UL
    )]
    [Arguments(
        0x7FF8123456789ABCUL,
        0xFFF8FEDCBA987654UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0x7FF0123456789ABCUL,
        0xFFF0FEDCBA987654UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    public async Task 正負の丸め境界と特殊値のlane_符号付きゼロを保ち整数値化する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2Ceil, operands);

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
