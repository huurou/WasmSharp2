using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I8x16ReplaceLaneTests
{
    [Test]
    [Arguments(0, 0x12345680, 0x7766554433221180UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(15, 0x1234567F, 0x7766554433221100UL, 0x7FEEDDCCBBAA9988UL)]
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I8x16ReplaceLane, operands, laneIndex);

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
