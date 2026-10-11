using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32WrapI64Tests
{
    [Test]
    [Arguments(unchecked((long)0x1234567880000000UL), int.MinValue)]
    [Arguments(unchecked((long)0xFFFFFFFF7FFFFFFFUL), int.MaxValue)]
    [Arguments(unchecked((long)0x8000000000000001UL), 1)]
    [Arguments(-1L, -1)]
    [Arguments(0x1234567800000000L, 0)]
    public async Task 上位bitと下位32bitを含む値_下位32bitだけを返す(long operand, int expected)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(operand)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32WrapI64, operands);

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
