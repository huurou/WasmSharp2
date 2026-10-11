using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64MaxTests
{
    [Test]
    [Arguments(0xBFF4000000000000ul, 0x401C000000000000ul, 0x401C000000000000ul)]
    [Arguments(0x401C000000000000ul, 0xBFF4000000000000ul, 0x401C000000000000ul)]
    [Arguments(0xBFF4000000000000ul, 0xBFF4000000000000ul, 0xBFF4000000000000ul)]
    [Arguments(0x0000000000000000ul, 0x8000000000000000ul, 0x0000000000000000ul)]
    [Arguments(0x8000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
    [Arguments(0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
    [Arguments(0x8000000000000000ul, 0x8000000000000000ul, 0x8000000000000000ul)]
    [Arguments(0xFFF0000000000000ul, 0x7FF0000000000000ul, 0x7FF0000000000000ul)]
    [Arguments(0x7FF0000000000000ul, 0xFFF0000000000000ul, 0x7FF0000000000000ul)]
    [Arguments(0x0000000000000001ul, 0x8000000000000001ul, 0x0000000000000001ul)]
    [Arguments(0x8000000000000001ul, 0x0000000000000001ul, 0x0000000000000001ul)]
    [Arguments(0x7FF8000000000000ul, 0xBFF4000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0xBFF4000000000000ul, 0x7FF8000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0xFFF8123456789ABCul, 0xBFF4000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0xBFF4000000000000ul, 0xFFF8123456789ABCul, 0x7FF8000000000000ul)]
    [Arguments(0x7FF0123456789ABCul, 0xBFF4000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0xBFF4000000000000ul, 0x7FF0123456789ABCul, 0x7FF8000000000000ul)]
    public async Task 通常値と等値と特殊値の左右operand_指定幅の最大値とNaN規則を返す(
        ulong leftBits,
        ulong rightBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(leftBits), WasmValue.FromF64Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64Max, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(actual.Value.AsF64Bits()).IsEqualTo(expectedBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
