using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4ShrSTests
{
    [Test]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        0,
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL
    )]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        32,
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL
    )]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        33,
        0x3FFFFFFFC0000000UL,
        0x00000000FFFFFFFFUL
    )]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        -1,
        0x00000000FFFFFFFFUL,
        0x00000000FFFFFFFFUL
    )]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        -2147483648,
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL
    )]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        1,
        0x3FFFFFFFC0000000UL,
        0x00000000FFFFFFFFUL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4ShrS, operands);

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
