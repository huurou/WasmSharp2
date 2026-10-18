using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4ConvertI32x4UTests
{
    [Test]
    [Arguments(
        0x0000000100000000UL,
        0x800000007FFFFFFFUL,
        0x3F80000000000000UL,
        0x4F0000004F000000UL
    )]
    [Arguments(
        0x80000080FFFFFFFFUL,
        0xFFFFFF7F80000081UL,
        0x4F0000004F800000UL,
        0x4F7FFFFF4F000001UL
    )]
    [Arguments(
        0x0100000000FFFFFFUL,
        0x0100000301000001UL,
        0x4B8000004B7FFFFFUL,
        0x4B8000024B800000UL
    )]
    [Arguments(
        0xFFFFFE81FFFFFE80UL,
        0x1234567880000082UL,
        0x4F7FFFFF4F7FFFFEUL,
        0x4D91A2B44F000001UL
    )]
    public async Task 符号bitと精度境界を含む整数lane_符号なし整数をf32へ最近接偶数で丸める(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4ConvertI32x4U, operands);

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
