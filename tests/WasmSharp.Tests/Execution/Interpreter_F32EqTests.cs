using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32EqTests
{
    [Test]
    [Arguments(0xBFA00000u, 0x40E00000u, 0)]
    [Arguments(0x40E00000u, 0xBFA00000u, 0)]
    [Arguments(0xBFA00000u, 0xBFA00000u, 1)]
    [Arguments(0x00000000u, 0x80000000u, 1)]
    [Arguments(0x80000000u, 0x00000000u, 1)]
    [Arguments(0x7F800000u, 0x7F800000u, 1)]
    [Arguments(0x00000001u, 0x00000000u, 0)]
    [Arguments(0xFFC12345u, 0xBFA00000u, 0)]
    [Arguments(0xBFA00000u, 0xFFC12345u, 0)]
    [Arguments(0x7F812345u, 0xBFA00000u, 0)]
    [Arguments(0xBFA00000u, 0x7F812345u, 0)]
    public async Task 通常値と等値とNaNの左右operand_i32で等値比較を返す(
        uint leftBits,
        uint rightBits,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(leftBits), WasmValue.FromF32Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Eq, operands);

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
