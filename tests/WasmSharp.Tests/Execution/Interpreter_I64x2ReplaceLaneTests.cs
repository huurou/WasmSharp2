using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2ReplaceLaneTests
{
    [Test]
    [Arguments(0, -9223372036854775808L, 0x8000000000000000UL, 0xFFEEDDCCBBAA9988UL)]
    [Arguments(1, 9223372036854775807L, 0x7766554433221100UL, 0x7FFFFFFFFFFFFFFFUL)]
    public async Task 最初と最後のlaneを置換_他のlaneの全bitを保持する(
        byte laneIndex,
        long operand,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(0x7766554433221100, 0xFFEEDDCCBBAA9988),
            WasmValue.FromI64(operand),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2ReplaceLane, operands, laneIndex);

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
