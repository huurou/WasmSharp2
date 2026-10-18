using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I8x16ShrUTests
{
    [Test]
    [Arguments(
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL,
        0,
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL
    )]
    [Arguments(
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL,
        8,
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL
    )]
    [Arguments(
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL,
        9,
        0x3F7F4001007F3F40UL,
        0x7D037D027E02017EUL
    )]
    [Arguments(
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL,
        -1,
        0x0001010000010001UL,
        0x0100010001000001UL
    )]
    [Arguments(
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL,
        -2147483648,
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL
    )]
    [Arguments(
        0x7EFE810201FF7F80UL,
        0xFA06FB05FC0403FDUL,
        1,
        0x3F7F4001007F3F40UL,
        0x7D037D027E02017EUL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I8x16ShrU, operands);

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
