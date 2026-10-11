using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32DivTests
{
    [Test]
    [Arguments(0x40E00000u, 0x40000000u, 0x40600000u)]
    [Arguments(0x3F800000u, 0x40400000u, 0x3EAAAAABu)]
    [Arguments(0x3F800000u, 0x00000000u, 0x7F800000u)]
    [Arguments(0x3F800000u, 0x80000000u, 0xFF800000u)]
    [Arguments(0x00000000u, 0x00000000u, 0x7FC00000u)]
    [Arguments(0x7F800000u, 0x7F800000u, 0x7FC00000u)]
    [Arguments(0x00000003u, 0x40000000u, 0x00000002u)]
    [Arguments(0x80000001u, 0x40000000u, 0x80000000u)]
    [Arguments(0xBF800000u, 0x7F800000u, 0x80000000u)]
    [Arguments(0xFFC12345u, 0x3F800000u, 0x7FC00000u)]
    public async Task 通常値と境界値とNaN_32bitの除算結果を返す(
        uint leftBits,
        uint rightBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(leftBits), WasmValue.FromF32Bits(rightBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Div, operands);

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
