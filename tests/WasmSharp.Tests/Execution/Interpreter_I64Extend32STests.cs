using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64Extend32STests
{
    [Test]
    [Arguments(0x123456787FFFFFFFL, 2147483647L)]
    [Arguments(unchecked((long)0xF234567880000000UL), -2147483648L)]
    [Arguments(0x12345678FFFFFFFFL, -1L)]
    [Arguments(unchecked((long)0x8765432100000000UL), 0L)]
    public async Task 上位bitと下位32bitの符号境界_下位32bitだけを符号拡張する(
        long operand,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64Extend32S, operands);

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
