using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2ConvertLowI32x4UTests
{
    [Test]
    [Arguments(
        0x80000000FFFFFFFFUL,
        0xDEADBEEF00000000UL,
        0x41EFFFFFFFE00000UL,
        0x41E0000000000000UL
    )]
    [Arguments(
        0x7FFFFFFF00000000UL,
        0x7654321081234567UL,
        0x0000000000000000UL,
        0x41DFFFFFFFC00000UL
    )]
    [Arguments(
        0x8000000101000001UL,
        0x01000003FFFFFFFFUL,
        0x4170000010000000UL,
        0x41E0000000200000UL
    )]
    public async Task 上位laneが異なる符号bitを含む整数vector_下位2laneだけを符号なし整数からf64へ正確に変換する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2ConvertLowI32x4U, operands);

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
