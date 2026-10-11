using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32NearestTests
{
    [Test]
    [Arguments(0x3F000000u, 0x00000000u)]
    [Arguments(0xBF000000u, 0x80000000u)]
    [Arguments(0x3FC00000u, 0x40000000u)]
    [Arguments(0xBFC00000u, 0xC0000000u)]
    [Arguments(0x40200000u, 0x40000000u)]
    [Arguments(0xC0200000u, 0xC0000000u)]
    [Arguments(0x3F000001u, 0x3F800000u)]
    [Arguments(0x3EFFFFFFu, 0x00000000u)]
    [Arguments(0xBF000001u, 0xBF800000u)]
    [Arguments(0xBEFFFFFFu, 0x80000000u)]
    [Arguments(0x00000000u, 0x00000000u)]
    [Arguments(0x80000000u, 0x80000000u)]
    [Arguments(0x00000001u, 0x00000000u)]
    [Arguments(0x80000001u, 0x80000000u)]
    [Arguments(0x4B000001u, 0x4B000001u)]
    [Arguments(0x7F800000u, 0x7F800000u)]
    [Arguments(0xFF800000u, 0xFF800000u)]
    [Arguments(0xFFC00000u, 0x7FC00000u)]
    [Arguments(0xFFC12345u, 0x7FC00000u)]
    [Arguments(0x7F812345u, 0x7FC00000u)]
    public async Task 入力の同距離と隣接値と特殊値_最近接偶数へ整数値化する(
        uint operandBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Nearest, operands);

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
