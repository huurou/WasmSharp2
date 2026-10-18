using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4ReplaceLaneTests
{
    [Test]
    [Arguments(0, 0x7F812345u, 0x776655447F812345UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(3, 0xFFC12345u, 0x7766554433221100UL, 0xFFC12345BBAA9988UL)]
    [Arguments(0, 0x80000000u, 0x7766554480000000UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(3, 0x00000000u, 0x7766554433221100UL, 0x00000000BBAA9988UL)]
    public async Task 最初と最後のlaneを置換_他のlaneの全bitを保持する(
        byte laneIndex,
        uint operand,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(0x7766554433221100, 0xFFEEDDCCBBAA9988),
            WasmValue.FromF32Bits(operand),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4ReplaceLane, operands, laneIndex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(actual.Value.AsV128()).IsEqualTo((expectedLow64, expectedHigh64));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
