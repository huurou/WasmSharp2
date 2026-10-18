using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4NegTests
{
    [Test]
    [Arguments(
        0x0000000080000000UL,
        0x7F800000FF800000UL,
        0x8000000000000000UL,
        0xFF8000007F800000UL
    )]
    [Arguments(
        0x7FC54321FF812345UL,
        0x3FC0000080000001UL,
        0xFFC543217F812345UL,
        0xBFC0000000000001UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Neg, operands);

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
