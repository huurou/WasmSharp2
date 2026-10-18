using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2SplatTests
{
    [Test]
    [Arguments(0x7FF0000000000123UL, 0x7FF0000000000123UL)]
    [Arguments(0xFFF8123456789ABCUL, 0xFFF8123456789ABCUL)]
    [Arguments(0x8000000000000000UL, 0x8000000000000000UL)]
    [Arguments(0x0000000000000000UL, 0x0000000000000000UL)]
    public async Task スカラーの境界bit_全laneへ同じbitを複製する(ulong operand, ulong expectedHalf)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operand)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2Splat, operands);

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
