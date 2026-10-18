using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2NegTests
{
    [Test]
    [Arguments(
        0x8000000000000000UL,
        0xFFFFFFFFFFFFFFFFUL,
        0x8000000000000000UL,
        0x0000000000000001UL
    )]
    [Arguments(
        0x0000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        0x0000000000000000UL,
        0x8000000000000001UL
    )]
    public async Task 最小負数と正負のlane_負数化を幅内へ折り返す(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2Neg, operands);

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
