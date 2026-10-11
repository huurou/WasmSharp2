using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64ExtendI32UTests
{
    [Test]
    [Arguments(0, 0L)]
    [Arguments(int.MaxValue, 2147483647L)]
    [Arguments(int.MinValue, 2147483648L)]
    [Arguments(-1, 4294967295L)]
    public async Task 上位bitを含む32bit値_ゼロを拡張して64bitで返す(int operand, long expected)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64ExtendI32U, operands);

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
}
