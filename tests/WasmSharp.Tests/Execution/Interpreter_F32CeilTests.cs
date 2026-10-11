using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32CeilTests
{
    [Test]
    [Arguments(0x3FA00000u, 0x40000000u)]
    [Arguments(0xBFA00000u, 0xBF800000u)]
    [Arguments(0xBE800000u, 0x80000000u)]
    [Arguments(0x00000000u, 0x00000000u)]
    [Arguments(0x80000000u, 0x80000000u)]
    [Arguments(0x00000001u, 0x3F800000u)]
    [Arguments(0x80000001u, 0x80000000u)]
    [Arguments(0x4B000001u, 0x4B000001u)]
    [Arguments(0x7F800000u, 0x7F800000u)]
    [Arguments(0xFF800000u, 0xFF800000u)]
    [Arguments(0xFFC00000u, 0x7FC00000u)]
    [Arguments(0xFFC12345u, 0x7FC00000u)]
    [Arguments(0x7F812345u, 0x7FC00000u)]
    public async Task 入力の通常値と境界値とNaN_正の無限大方向へ整数値化する(
        uint operandBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Ceil, operands);

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
