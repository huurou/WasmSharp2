using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4DemoteF64x2ZeroTests
{
    [Test]
    [Arguments(
        0x3FF8000000000000UL,
        0xBFF8000000000000UL,
        0xBFC000003FC00000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x3FF0000010000000UL,
        0x3FF0000030000000UL,
        0x3F8000023F800000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xBFF0000010000000UL,
        0xBFF0000030000000UL,
        0xBF800002BF800000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x3FF000000FFFFFFFUL,
        0x3FF0000010000001UL,
        0x3F8000013F800000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x0000000000000000UL,
        0x8000000000000000UL,
        0x8000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF0000000000000UL,
        0xFFF0000000000000UL,
        0xFF8000007F800000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x0000000000000001UL,
        0x8000000000000001UL,
        0x8000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x36A0000000000000UL,
        0xB6A0000000000000UL,
        0x8000000100000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x3690000000000000UL,
        0xB690000000000000UL,
        0x8000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x3690000000000001UL,
        0xB690000000000001UL,
        0x8000000100000001UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x36A8000000000000UL,
        0xB6A8000000000000UL,
        0x8000000200000002UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x380FFFFFC0000000UL,
        0x3810000000000000UL,
        0x00800000007FFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x47EFFFFFE0000000UL,
        0xC7EFFFFFE0000000UL,
        0xFF7FFFFF7F7FFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x47EFFFFFEFFFFFFFUL,
        0x47EFFFFFF0000000UL,
        0x7F8000007F7FFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FEFFFFFFFFFFFFFUL,
        0xFFEFFFFFFFFFFFFFUL,
        0xFF8000007F800000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF8000000000000UL,
        0xFFF8000000000000UL,
        0x7FC000007FC00000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FF8123456789ABCUL,
        0xFFF0123456789ABCUL,
        0x7FC000007FC00000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xFFF8FEDCBA987654UL,
        0x7FF0FEDCBA987654UL,
        0x7FC000007FC00000UL,
        0x0000000000000000UL
    )]
    public async Task 丸め境界と特殊値のf64lane_f32へ丸めて上位2laneをゼロにする(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4DemoteF64x2Zero, operands);

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
