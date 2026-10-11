using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64DivSTests
{
    [Test]
    [Arguments(7L, 3L, 2L)]
    [Arguments(-7L, 3L, -2L)]
    [Arguments(7L, -3L, -2L)]
    [Arguments(-7L, -3L, 2L)]
    [Arguments(3L, 11L, 0L)]
    [Arguments(long.MinValue, 1L, long.MinValue)]
    [Arguments(0L, -1L, 0L)]
    public async Task 正負と左右が異なる値と幅境界_左を右で割った0方向への整数商を返す(
        long left,
        long right,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(left), WasmValue.FromI64(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64DivS, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI64(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0L, 0L, WasmTrapReason.IntegerDivideByZero)]
    [Arguments(long.MinValue, 0L, WasmTrapReason.IntegerDivideByZero)]
    [Arguments(long.MinValue, -1L, WasmTrapReason.IntegerOverflow)]
    public async Task 除数が0または符号付き最小値を負1で割る場合_原因と関数と命令位置を持つtrapを返し下位の値を保つ(
        long left,
        long right,
        WasmTrapReason expectedReason
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(left), WasmValue.FromI64(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64DivS, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(actual.Result.TrapReason).IsEqualTo(expectedReason);
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }
}
