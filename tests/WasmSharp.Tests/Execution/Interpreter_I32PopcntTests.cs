using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32PopcntTests
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(-1, 32)]
    [Arguments(unchecked((int)0x80000000), 1)]
    [Arguments(unchecked((int)0xA5A5A5A5), 16)]
    public async Task 入力0と全bit1と最上位bitを含む値_1の個数を元の整数幅で返す(
        int operand,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32Popcnt, operands);

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
}
