using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64PopcntTests
{
    [Test]
    [Arguments(0L, 0L)]
    [Arguments(-1L, 64L)]
    [Arguments(unchecked((long)0x8000000000000000UL), 1L)]
    [Arguments(unchecked((long)0xA5A5A5A5A5A5A5A5UL), 32L)]
    [Arguments(unchecked((long)0xFFFFFFFF00000000UL), 32L)]
    public async Task 入力0と全bit1と最上位bitを含む値_1の個数を元の整数幅で返す(
        long operand,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64Popcnt, operands);

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
