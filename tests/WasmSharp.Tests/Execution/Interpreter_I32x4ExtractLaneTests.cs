using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4ExtractLaneTests
{
    [Test]
    [Arguments(0, 0x7766554480000000UL, 0xFFFFFFFFBBAA9988UL, -2147483648)]
    [Arguments(3, 0x7766554480000000UL, 0xFFFFFFFFBBAA9988UL, -1)]
    [Arguments(3, 0x7766554480000000UL, 0x7FFFFFFFBBAA9988UL, 2147483647)]
    public async Task 最初と最後のlaneの境界bit_指定スカラー型で取り出す(
        byte laneIndex,
        ulong low64,
        ulong high64,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4ExtractLane, operands, laneIndex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
