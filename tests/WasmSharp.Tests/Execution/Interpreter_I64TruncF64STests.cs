using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64TruncF64STests
{
    [Test]
    [Arguments(0x401F99999999999AUL, 7L)]
    [Arguments(0x0000000000000000UL, 0L)]
    [Arguments(0x8000000000000000UL, 0L)]
    [Arguments(0xBFECCCCCCCCCCCCDUL, 0L)]
    [Arguments(0xBFEFFFFFFFFFFFFFUL, 0L)]
    [Arguments(0x0000000000000001UL, 0L)]
    [Arguments(0x8000000000000001UL, 0L)]
    [Arguments(0xC01F99999999999AUL, -7L)]
    [Arguments(0x43DFFFFFFFFFFFFFUL, 9223372036854774784L)]
    [Arguments(0xC3E0000000000000UL, long.MinValue)]
    [Arguments(0xC3DFFFFFFFFFFFFFUL, -9223372036854774784L)]
    public async Task 入力が切捨て後の範囲内_0方向への整数値を同幅のbit列で返す(
        ulong operandBits,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64TruncF64S, operands);

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
    [Arguments(0x43E0000000000000UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0x43E0000000000001UL, WasmTrapReason.IntegerOverflow)]
    [Arguments(0xC3E0000000000001UL, WasmTrapReason.IntegerOverflow)]
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
        var actual = NumericHandlerFixture.Execute(Interpreter.I64TruncF64S, operands);

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
