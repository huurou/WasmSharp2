using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2NegTests
{
    [Test]
    [Arguments(
        0x8000000000000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL,
        0x8000000000000000UL
    )]
    [Arguments(
        0xFFF0000000000000UL,
        0x7FF0000000000000UL,
        0x7FF0000000000000UL,
        0xFFF0000000000000UL
    )]
    [Arguments(
        0xFFF0123456789ABCUL,
        0x7FF8ABCDEF123456UL,
        0x7FF0123456789ABCUL,
        0xFFF8ABCDEF123456UL
    )]
    [Arguments(
        0x8000000000000001UL,
        0x3FF8000000000000UL,
        0x0000000000000001UL,
        0xBFF8000000000000UL
    )]
    public async Task 符号とNaNpayloadを含むlane_符号bitだけを反転する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2Neg, operands);

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
