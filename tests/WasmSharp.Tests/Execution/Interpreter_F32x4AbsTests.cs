using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4AbsTests
{
    [Test]
    [Arguments(
        0xFF80000080000000UL,
        0x7FC54321FF812345UL,
        0x7F80000000000000UL,
        0x7FC543217F812345UL
    )]
    [Arguments(
        0x0000000180000001UL,
        0x40100000BFC00000UL,
        0x0000000100000001UL,
        0x401000003FC00000UL
    )]
    public async Task 符号とNaNpayloadを含むlane_符号bitだけをゼロにする(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Abs, operands);

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
