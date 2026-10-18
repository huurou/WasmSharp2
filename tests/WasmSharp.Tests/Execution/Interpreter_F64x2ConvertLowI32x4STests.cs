using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2ConvertLowI32x4STests
{
    [Test]
    [Arguments(
        0x7FFFFFFFFFFFFFFFUL,
        0x1234567880000000UL,
        0xBFF0000000000000UL,
        0x41DFFFFFFFC00000UL
    )]
    [Arguments(
        0x0000000080000000UL,
        0xFFFFFFFF7FFFFFFFUL,
        0xC1E0000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xFEFFFFFF01000001UL,
        0xBF8000003F800000UL,
        0x4170000010000000UL,
        0xC170000010000000UL
    )]
    public async Task 上位laneが異なる符号境界の整数vector_下位2laneだけを符号付き整数からf64へ正確に変換する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2ConvertLowI32x4S, operands);

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
