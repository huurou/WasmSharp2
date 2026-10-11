using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32TruncF64UTests
{
    [Test]
    [Arguments(0x401F99999999999AUL, 7)]
    [Arguments(0x0000000000000000UL, 0)]
    [Arguments(0x8000000000000000UL, 0)]
    [Arguments(0xBFECCCCCCCCCCCCDUL, 0)]
    [Arguments(0xBFEFFFFFFFFFFFFFUL, 0)]
    [Arguments(0x0000000000000001UL, 0)]
    [Arguments(0x8000000000000001UL, 0)]
    [Arguments(0x41E0000000000000UL, int.MinValue)]
    [Arguments(0x41EFFFFFFFFFFFFFUL, -1)]
    public async Task 入力が切捨て後の範囲内_0方向への整数値を同幅のbit列で返す(
        ulong operandBits,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32TruncF64U, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0x41F0000000000000UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x41F0000000000001UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xBFF0000000000000UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xBFF0000000000001UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xC01F99999999999AUL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x7FF0000000000000UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xFFF0000000000000UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x7FF8000000000000UL, WasmTrapReason.InvalidConversionToInteger)]
    [Arguments(0xFFF8123456789ABCUL, WasmTrapReason.InvalidConversionToInteger)]
    [Arguments(0x7FF0123456789ABCUL, WasmTrapReason.InvalidConversionToInteger)]
    public async Task 入力がNaNまたは切捨て後の範囲外_原因と位置を持つtrapを返し下位の値を保つ(
        ulong operandBits,
        WasmTrapReason expectedReason
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32TruncF64U, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(actual.Result.TrapReason).IsEqualTo(expectedReason);
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.Value).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }
}
