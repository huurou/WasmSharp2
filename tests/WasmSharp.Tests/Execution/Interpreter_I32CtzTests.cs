using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32CtzTests
{
    [Test]
    [Arguments(0, 32)]
    [Arguments(-1, 0)]
    [Arguments(unchecked((int)0x80000000), 31)]
    [Arguments(1, 0)]
    [Arguments(unchecked((int)0x00010000), 16)]
    public async Task 入力0と全bit1と最上位bitを含む値_末尾ゼロ数を元の整数幅で返す(
        int operand,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32Ctz, operands);

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
