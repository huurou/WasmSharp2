using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64GeTests
{
    [Test]
    [Arguments(0xBFF4000000000000ul, 0x401C000000000000ul, 0)]
    [Arguments(0x401C000000000000ul, 0xBFF4000000000000ul, 1)]
    [Arguments(0xBFF4000000000000ul, 0xBFF4000000000000ul, 1)]
    [Arguments(0x0000000000000000ul, 0x8000000000000000ul, 1)]
    [Arguments(0x8000000000000000ul, 0x0000000000000000ul, 1)]
    [Arguments(0x7FF0000000000000ul, 0x7FF0000000000000ul, 1)]
    [Arguments(0x0000000000000001ul, 0x0000000000000000ul, 1)]
    [Arguments(0xFFF8123456789ABCul, 0xBFF4000000000000ul, 0)]
    [Arguments(0xBFF4000000000000ul, 0xFFF8123456789ABCul, 0)]
    [Arguments(0x7FF0123456789ABCul, 0xBFF4000000000000ul, 0)]
    [Arguments(0xBFF4000000000000ul, 0x7FF0123456789ABCul, 0)]
    public async Task 通常値と等値とNaNの左右operand_i32で以上比較を返す(
        ulong leftBits,
        ulong rightBits,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(leftBits), WasmValue.FromF64Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64Ge, operands);

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
