using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32ReinterpretI32Tests
{
    [Test]
    [Arguments(0x00000000u)]
    [Arguments(0x80000000u)]
    [Arguments(0x3F800001u)]
    [Arguments(0x7F812345u)]
    [Arguments(0xFF812345u)]
    [Arguments(0x7FC54321u)]
    [Arguments(0xFFC54321u)]
    public async Task 通常値と符号付き0と生NaN_全bitを保持して再解釈する(uint operandBits)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(unchecked((int)operandBits))];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32ReinterpretI32, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(operandBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0x00000000u)]
    [Arguments(0x80000000u)]
    [Arguments(0xFF812345u)]
    [Arguments(0x7FC54321u)]
    public async Task 符号付き0と生NaNを往復する場合_元の整数bitを保持する(uint operandBits)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(unchecked((int)operandBits))];

        // Act
        var actual = NumericHandlerFixture.Execute(
            static (context, in instruction) =>
            {
                var result = Interpreter.F32ReinterpretI32(context, in instruction);
                if (result.Status != ExecutionStatus.Success)
                {
                    return result;
                }

                return Interpreter.I32ReinterpretF32(context, in instruction);
            },
            operands
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(unchecked((uint)actual.Value.AsI32())).IsEqualTo(operandBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
