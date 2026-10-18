using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2ShrSTests
{
    [Test]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        0,
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        64,
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        65,
        0xC000000000000000UL,
        0x3FFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        -1,
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        -2147483648,
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        1,
        0xC000000000000000UL,
        0x3FFFFFFFFFFFFFFFUL
    )]
    public async Task シフト量がゼロや幅以上や負数_幅の剰余で各laneをシフトする(
        ulong inputLow64,
        ulong inputHigh64,
        int shift,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(inputLow64, inputHigh64),
            WasmValue.FromI32(shift),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2ShrS, operands);

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
