using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2ExtractLaneTests
{
    [Test]
    [Arguments(0, 0x7FF0000000000123UL, 0xFFEEDDCCBBAA9988UL, 0x7FF0000000000123UL)]
    [Arguments(1, 0x7766554433221100UL, 0xFFF8123456789ABCUL, 0xFFF8123456789ABCUL)]
    [Arguments(0, 0x8000000000000000UL, 0xFFEEDDCCBBAA9988UL, 0x8000000000000000UL)]
    [Arguments(1, 0x7766554433221100UL, 0x0000000000000000UL, 0x0000000000000000UL)]
    public async Task 最初と最後のlaneの境界bit_指定スカラー型で取り出す(
        byte laneIndex,
        ulong low64,
        ulong high64,
        ulong expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2ExtractLane, operands, laneIndex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(actual.Value.AsF64Bits()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
