using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I8x16SplatTests
{
    [Test]
    [Arguments(0x12345680, 0x8080808080808080UL)]
    [Arguments(-1, 0xFFFFFFFFFFFFFFFFUL)]
    [Arguments(0x100, 0x0000000000000000UL)]
    public async Task スカラーの境界bit_全laneへ同じbitを複製する(int operand, ulong expectedHalf)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(operand)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I8x16Splat, operands);

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
