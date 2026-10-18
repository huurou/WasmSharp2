using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4ConvertI32x4STests
{
    [Test]
    [Arguments(
        0xFFFFFFFF00000000UL,
        0x800000007FFFFFFFUL,
        0xBF80000000000000UL,
        0xCF0000004F000000UL
    )]
    [Arguments(
        0x0100000000FFFFFFUL,
        0x0100000301000001UL,
        0x4B8000004B7FFFFFUL,
        0x4B8000024B800000UL
    )]
    [Arguments(
        0xFF000000FF000001UL,
        0xFEFFFFFDFEFFFFFFUL,
        0xCB800000CB7FFFFFUL,
        0xCB800002CB800000UL
    )]
    [Arguments(
        0x7FFFFFC07FFFFFBFUL,
        0x8000004080000041UL,
        0x4F0000004EFFFFFFUL,
        0xCF000000CEFFFFFFUL
    )]
    public async Task 符号境界と精度境界の整数lane_符号付き整数をf32へ最近接偶数で丸める(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4ConvertI32x4S, operands);

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
