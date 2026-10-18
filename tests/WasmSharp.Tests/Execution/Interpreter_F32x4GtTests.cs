using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4GtTests
{
    [Test]
    [Arguments(
        0x3F8000007FC12345UL,
        0x8000000000000000UL,
        0xFFC543213F800000UL,
        0x0000000080000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7F800000FF800000UL,
        0x40800000C0000000UL,
        0xFF8000007F800000UL,
        0x40400000BF800000UL,
        0xFFFFFFFF00000000UL,
        0xFFFFFFFF00000000UL
    )]
    [Arguments(
        0x7F800000FF800000UL,
        0xC040000040000000UL,
        0x7F800000FF800000UL,
        0xC040000040000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    public async Task NaNとゼロと無限大を含むlane_比較結果を全bit1か0で返す(
        ulong firstLow64,
        ulong firstHigh64,
        ulong secondLow64,
        ulong secondHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(firstLow64, firstHigh64),
            WasmValue.FromV128(secondLow64, secondHigh64),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Gt, operands);

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
