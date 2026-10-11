using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32NegTests
{
    [Test]
    [Arguments(0x3FA00000u, 0xBFA00000u)]
    [Arguments(0xBFA00000u, 0x3FA00000u)]
    [Arguments(0x00000000u, 0x80000000u)]
    [Arguments(0x80000000u, 0x00000000u)]
    [Arguments(0x7F800000u, 0xFF800000u)]
    [Arguments(0xFF800000u, 0x7F800000u)]
    [Arguments(0x7FC12345u, 0xFFC12345u)]
    [Arguments(0xFF812345u, 0x7F812345u)]
    [Arguments(0x00000001u, 0x80000001u)]
    public async Task 入力の符号と生NaN_符号bitだけを反転する(uint operandBits, uint expectedBits)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Neg, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(expectedBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
