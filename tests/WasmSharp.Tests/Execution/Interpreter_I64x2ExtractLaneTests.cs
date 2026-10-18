using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2ExtractLaneTests
{
    [Test]
    [Arguments(0, 0x8000000000000000UL, 0xFFFFFFFFFFFFFFFFUL, -9223372036854775808L)]
    [Arguments(1, 0x8000000000000000UL, 0xFFFFFFFFFFFFFFFFUL, -1L)]
    [Arguments(1, 0x8000000000000000UL, 0x7FFFFFFFFFFFFFFFUL, 9223372036854775807L)]
    public async Task 最初と最後のlaneの境界bit_指定スカラー型で取り出す(
        byte laneIndex,
        ulong low64,
        ulong high64,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2ExtractLane, operands, laneIndex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(actual.Value.AsI64()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
