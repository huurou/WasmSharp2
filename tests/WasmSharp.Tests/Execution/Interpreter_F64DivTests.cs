using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64DivTests
{
    [Test]
    [Arguments(0x401C000000000000ul, 0x4000000000000000ul, 0x400C000000000000ul)]
    [Arguments(0x3FF0000000000000ul, 0x4008000000000000ul, 0x3FD5555555555555ul)]
    [Arguments(0x3FF0000000000000ul, 0x0000000000000000ul, 0x7FF0000000000000ul)]
    [Arguments(0x3FF0000000000000ul, 0x8000000000000000ul, 0xFFF0000000000000ul)]
    [Arguments(0x0000000000000000ul, 0x0000000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0x7FF0000000000000ul, 0x7FF0000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0x0000000000000003ul, 0x4000000000000000ul, 0x0000000000000002ul)]
    [Arguments(0x8000000000000001ul, 0x4000000000000000ul, 0x8000000000000000ul)]
    [Arguments(0xBFF0000000000000ul, 0x7FF0000000000000ul, 0x8000000000000000ul)]
    [Arguments(0xFFF8123456789ABCul, 0x3FF0000000000000ul, 0x7FF8000000000000ul)]
    public async Task 通常値と境界値とNaN_64bitの除算結果を返す(
        ulong leftBits,
        ulong rightBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(leftBits), WasmValue.FromF64Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64Div, operands);

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
