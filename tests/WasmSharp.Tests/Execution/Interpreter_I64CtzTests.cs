using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64CtzTests
{
    [Test]
    [Arguments(0L, 64L)]
    [Arguments(-1L, 0L)]
    [Arguments(unchecked((long)0x8000000000000000UL), 63L)]
    [Arguments(1L, 0L)]
    [Arguments(unchecked((long)0x0001000000000000UL), 48L)]
    public async Task 入力0と全bit1と最上位bitを含む値_末尾ゼロ数を元の整数幅で返す(
        long operand,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64Ctz, operands);

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
}
