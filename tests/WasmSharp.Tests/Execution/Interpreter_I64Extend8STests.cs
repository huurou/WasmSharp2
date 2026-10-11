using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64Extend8STests
{
    [Test]
    [Arguments(0x123456789ABCDE7FL, 127L)]
    [Arguments(unchecked((long)0xF23456789ABCDE80UL), -128L)]
    [Arguments(0x123456789ABCDEFFL, -1L)]
    [Arguments(unchecked((long)0x8765432100000000UL), 0L)]
    public async Task 上位bitと下位8bitの符号境界_下位8bitだけを符号拡張する(
        long operand,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64Extend8S, operands);

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
