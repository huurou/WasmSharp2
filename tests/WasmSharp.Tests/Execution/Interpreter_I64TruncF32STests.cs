using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64TruncF32STests
{
    [Test]
    [Arguments(0x40FCCCCDU, 7L)]
    [Arguments(0x00000000U, 0L)]
    [Arguments(0x80000000U, 0L)]
    [Arguments(0xBF666666U, 0L)]
    [Arguments(0xBF7FFFFFU, 0L)]
    [Arguments(0x00000001U, 0L)]
    [Arguments(0x80000001U, 0L)]
    [Arguments(0xC0FCCCCDU, -7L)]
    [Arguments(0x5EFFFFFFU, 9223371487098961920L)]
    [Arguments(0xDF000000U, long.MinValue)]
    [Arguments(0xDEFFFFFFU, -9223371487098961920L)]
    public async Task 入力が切捨て後の範囲内_0方向への整数値を同幅のbit列で返す(
        uint operandBits,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64TruncF32S, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(actual.Value.AsI64()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0x5F000000U, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x5F000001U, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xDF000001U, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x7F800000U, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xFF800000U, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x7FC00000U, WasmTrapReason.InvalidConversionToInteger)]
    [Arguments(0xFFC12345U, WasmTrapReason.InvalidConversionToInteger)]
    [Arguments(0x7F812345U, WasmTrapReason.InvalidConversionToInteger)]
    public async Task 入力がNaNまたは切捨て後の範囲外_原因と位置を持つtrapを返し下位の値を保つ(
        uint operandBits,
        WasmTrapReason expectedReason
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64TruncF32S, operands);

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
