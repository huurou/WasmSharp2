using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64NearestTests
{
    [Test]
    [Arguments(0x3FE0000000000000ul, 0x0000000000000000ul)]
    [Arguments(0xBFE0000000000000ul, 0x8000000000000000ul)]
    [Arguments(0x3FF8000000000000ul, 0x4000000000000000ul)]
    [Arguments(0xBFF8000000000000ul, 0xC000000000000000ul)]
    [Arguments(0x4004000000000000ul, 0x4000000000000000ul)]
    [Arguments(0xC004000000000000ul, 0xC000000000000000ul)]
    [Arguments(0x3FE0000000000001ul, 0x3FF0000000000000ul)]
    [Arguments(0x3FDFFFFFFFFFFFFFul, 0x0000000000000000ul)]
    [Arguments(0xBFE0000000000001ul, 0xBFF0000000000000ul)]
    [Arguments(0xBFDFFFFFFFFFFFFFul, 0x8000000000000000ul)]
    [Arguments(0x0000000000000000ul, 0x0000000000000000ul)]
    [Arguments(0x8000000000000000ul, 0x8000000000000000ul)]
    [Arguments(0x0000000000000001ul, 0x0000000000000000ul)]
    [Arguments(0x8000000000000001ul, 0x8000000000000000ul)]
    [Arguments(0x4330000000000001ul, 0x4330000000000001ul)]
    [Arguments(0x7FF0000000000000ul, 0x7FF0000000000000ul)]
    [Arguments(0xFFF0000000000000ul, 0xFFF0000000000000ul)]
    [Arguments(0xFFF8000000000000ul, 0x7FF8000000000000ul)]
    [Arguments(0xFFF8123456789ABCul, 0x7FF8000000000000ul)]
    [Arguments(0x7FF0123456789ABCul, 0x7FF8000000000000ul)]
    public async Task 入力の同距離と隣接値と特殊値_最近接偶数へ整数値化する(
        ulong operandBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64Nearest, operands);

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
