using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32DivSTests
{
    [Test]
    [Arguments(7, 3, 2)]
    [Arguments(-7, 3, -2)]
    [Arguments(7, -3, -2)]
    [Arguments(-7, -3, 2)]
    [Arguments(3, 11, 0)]
    [Arguments(int.MinValue, 1, int.MinValue)]
    [Arguments(0, -1, 0)]
    public async Task 正負と左右が異なる値と幅境界_左を右で割った0方向への整数商を返す(
        int left,
        int right,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(left), WasmValue.FromI32(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32DivS, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI32(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0, 0, WasmTrapReason.IntegerDivideByZero)]
    [Arguments(int.MinValue, 0, WasmTrapReason.IntegerDivideByZero)]
    [Arguments(int.MinValue, -1, WasmTrapReason.IntegerOverflow)]
    public async Task 除数が0または符号付き最小値を負1で割る場合_原因と関数と命令位置を持つtrapを返し下位の値を保つ(
        int left,
        int right,
        WasmTrapReason expectedReason
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(left), WasmValue.FromI32(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32DivS, operands);

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
