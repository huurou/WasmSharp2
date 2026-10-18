using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2ReplaceLaneTests
{
    [Test]
    [Arguments(0, 0x7FF0000000000123UL, 0x7FF0000000000123UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(1, 0xFFF8123456789ABCUL, 0x7766554433221100UL, 0xFFF8123456789ABCUL)]
    [Arguments(0, 0x8000000000000000UL, 0x8000000000000000UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(1, 0x0000000000000000UL, 0x7766554433221100UL, 0x0000000000000000UL)]
    public async Task 最初と最後のlaneを置換_他のlaneの全bitを保持する(
        byte laneIndex,
        ulong operand,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(0x7766554433221100, 0xFFEEDDCCBBAA9988),
            WasmValue.FromF64Bits(operand),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2ReplaceLane, operands, laneIndex);

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
