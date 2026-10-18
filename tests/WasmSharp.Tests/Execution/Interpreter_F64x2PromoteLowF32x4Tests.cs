using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2PromoteLowF32x4Tests
{
    [Test]
    [Arguments(
        0xBFC000003FC00000UL,
        0xFF8123457F800000UL,
        0x3FF8000000000000UL,
        0xBFF8000000000000UL
    )]
    [Arguments(
        0x8000000000000000UL,
        0x3F8000007FC54321UL,
        0x0000000000000000UL,
        0x8000000000000000UL
    )]
    [Arguments(
        0x8000000100000001UL,
        0xFF8000007F7FFFFFUL,
        0x36A0000000000000UL,
        0xB6A0000000000000UL
    )]
    [Arguments(
        0x00800000007FFFFFUL,
        0xDEADBEEF80000001UL,
        0x380FFFFFC0000000UL,
        0x3810000000000000UL
    )]
    [Arguments(
        0xFF7FFFFF7F7FFFFFUL,
        0x1234567880000000UL,
        0x47EFFFFFE0000000UL,
        0xC7EFFFFFE0000000UL
    )]
    [Arguments(
        0xFF8000007F800000UL,
        0x7F8123453F800001UL,
        0x7FF0000000000000UL,
        0xFFF0000000000000UL
    )]
    [Arguments(
        0xBF8000033F800001UL,
        0x00000001FF800000UL,
        0x3FF0000020000000UL,
        0xBFF0000060000000UL
    )]
    [Arguments(
        0xFFC000007FC00000UL,
        0x7F8000003FC00000UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0xFF8123457FC12345UL,
        0xBF8000003F800000UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0x7F854321FFC54321UL,
        0x800000007F800000UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    public async Task 上位laneが異なる特殊値と精度境界のf32vector_下位2laneだけをf64へ拡張する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2PromoteLowF32x4, operands);

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
