using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32Extend8STests
{
    [Test]
    [Arguments(0x1234567F, 127)]
    [Arguments(0x12345680, -128)]
    [Arguments(0x123456FF, -1)]
    [Arguments(unchecked((int)0x87654300u), 0)]
    public async Task 上位bitと下位8bitの符号境界_下位8bitだけを符号拡張する(
        int operand,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32Extend8S, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
