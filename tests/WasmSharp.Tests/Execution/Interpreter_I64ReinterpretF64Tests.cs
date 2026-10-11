using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64ReinterpretF64Tests
{
    [Test]
    [Arguments(0x0000000000000000UL)]
    [Arguments(0x8000000000000000UL)]
    [Arguments(0x3FF0000000000001UL)]
    [Arguments(0x7FF0123456789ABCUL)]
    [Arguments(0xFFF0123456789ABCUL)]
    [Arguments(0x7FF8543212345678UL)]
    [Arguments(0xFFF8543212345678UL)]
    public async Task 通常値と符号付き0と生NaN_全bitを保持して再解釈する(ulong operandBits)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64ReinterpretF64, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(unchecked((ulong)actual.Value.AsI64())).IsEqualTo(operandBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
