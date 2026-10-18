using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8ReplaceLaneTests
{
    [Test]
    [Arguments(0, 0x12348000, 0x7766554433228000UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(7, 0x12347FFF, 0x7766554433221100UL, 0x7FFFDDCCBBAA9988UL)]
    public async Task 最初と最後のlaneを置換_他のlaneの全bitを保持する(
        byte laneIndex,
        int operand,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(0x7766554433221100, 0xFFEEDDCCBBAA9988),
            WasmValue.FromI32(operand),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8ReplaceLane, operands, laneIndex);

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
