using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32TruncSatF64STests
{
    [Test]
    [Arguments(0x401F99999999999AUL, 7)]
    [Arguments(0x0000000000000000UL, 0)]
    [Arguments(0x8000000000000000UL, 0)]
    [Arguments(0xBFECCCCCCCCCCCCDUL, 0)]
    [Arguments(0xBFEFFFFFFFFFFFFFUL, 0)]
    [Arguments(0x0000000000000001UL, 0)]
    [Arguments(0x8000000000000001UL, 0)]
    [Arguments(0xC01F99999999999AUL, -7)]
    [Arguments(0x41DFFFFFFFFFFFFFUL, int.MaxValue)]
    [Arguments(0xC1E0000000000000UL, int.MinValue)]
    [Arguments(0xC1DFFFFFFFFFFFFFUL, -2147483647)]
    [Arguments(0xC1E0000000000001UL, int.MinValue)]
    // −2147483648.9は切捨て後に範囲へ入る
    [Arguments(0xC1E00000001CCCCDUL, int.MinValue)]
    [Arguments(0x41E0000000000000UL, int.MaxValue)]
    [Arguments(0x41E0000000000001UL, int.MaxValue)]
    [Arguments(0xC1E0000000200000UL, int.MinValue)]
    [Arguments(0x7FF0000000000000UL, int.MaxValue)]
    [Arguments(0xFFF0000000000000UL, int.MinValue)]
    [Arguments(0x7FF8000000000000UL, 0)]
    [Arguments(0xFFF8123456789ABCUL, 0)]
    [Arguments(0x7FF0123456789ABCUL, 0)]
    public async Task 入力が有限値または特殊値_切捨てと飽和した整数をtrapなしで返す(
        ulong operandBits,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32TruncSatF64S, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Result.TrapReason).IsNull();
            await Assert.That(actual.Result.FunctionIndex).IsNull();
            await Assert.That(actual.Result.ByteOffset).IsNull();
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI32(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
