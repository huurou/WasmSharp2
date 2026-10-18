using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4SplatTests
{
    [Test]
    [Arguments(0x7F812345u, 0x7F8123457F812345UL)]
    [Arguments(0xFFC12345u, 0xFFC12345FFC12345UL)]
    [Arguments(0x80000000u, 0x8000000080000000UL)]
    [Arguments(0x00000000u, 0x0000000000000000UL)]
    public async Task スカラーの境界bit_全laneへ同じbitを複製する(uint operand, ulong expectedHalf)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operand)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Splat, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(actual.Value.AsV128()).IsEqualTo((expectedHalf, expectedHalf));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
