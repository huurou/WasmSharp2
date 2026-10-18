using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4ExtractLaneTests
{
    [Test]
    [Arguments(0, 0x776655447F812345UL, 0xFFEEDDCCBBAA9988UL, 0x7F812345u)]
    [Arguments(3, 0x7766554433221100UL, 0xFFC12345BBAA9988UL, 0xFFC12345u)]
    [Arguments(0, 0x7766554480000000UL, 0xFFEEDDCCBBAA9988UL, 0x80000000u)]
    [Arguments(3, 0x7766554433221100UL, 0x00000000BBAA9988UL, 0x00000000u)]
    public async Task 最初と最後のlaneの境界bit_指定スカラー型で取り出す(
        byte laneIndex,
        ulong low64,
        ulong high64,
        uint expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4ExtractLane, operands, laneIndex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
