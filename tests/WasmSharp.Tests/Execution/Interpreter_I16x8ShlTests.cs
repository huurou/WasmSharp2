using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8ShlTests
{
    [Test]
    [Arguments(
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL,
        0,
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL
    )]
    [Arguments(
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL,
        16,
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL
    )]
    [Arguments(
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL,
        17,
        0x0002FFFEFFFE0000UL,
        0xFFFCFFFC00020004UL
    )]
    [Arguments(
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL,
        -1,
        0x8000800080000000UL,
        0x0000000080000000UL
    )]
    [Arguments(
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL,
        -2147483648,
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL
    )]
    [Arguments(
        0x0001FFFF7FFF8000UL,
        0x7FFEFFFE80010002UL,
        1,
        0x0002FFFEFFFE0000UL,
        0xFFFCFFFC00020004UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8Shl, operands);

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
