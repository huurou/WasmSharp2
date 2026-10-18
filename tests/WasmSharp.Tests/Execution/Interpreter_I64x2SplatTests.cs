using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2SplatTests
{
    [Test]
    [Arguments(-9223372036854775808L, 0x8000000000000000UL)]
    [Arguments(-1L, 0xFFFFFFFFFFFFFFFFUL)]
    public async Task スカラーの境界bit_全laneへ同じbitを複製する(long operand, ulong expectedHalf)
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(operand)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2Splat, operands);

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
