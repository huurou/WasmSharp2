using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32Extend16STests
{
    [Test]
    [Arguments(0x12347FFF, 32767)]
    [Arguments(0x12348000, -32768)]
    [Arguments(0x1234FFFF, -1)]
    [Arguments(unchecked((int)0x87650000u), 0)]
    public async Task 上位bitと下位16bitの符号境界_下位16bitだけを符号拡張する(
        int operand,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32Extend16S, operands);

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
