using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64x2MaxTests
{
    [Test]
    [Arguments(
        0x0000000000000000UL,
        0x8000000000000000UL,
        0x8000000000000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x4008000000000000UL,
        0xC000000000000000UL,
        0xC000000000000000UL,
        0x4008000000000000UL,
        0x4008000000000000UL,
        0x4008000000000000UL
    )]
    [Arguments(
        0x7FF8123456789ABCUL,
        0x4008000000000000UL,
        0x4008000000000000UL,
        0xFFF8FEDCBA987654UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0x7FF0123456789ABCUL,
        0xBFF0000000000000UL,
        0xBFF0000000000000UL,
        0xFFF0FEDCBA987654UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0x7FF0000000000000UL,
        0xFFF0000000000000UL,
        0xFFF0000000000000UL,
        0x7FF0000000000000UL,
        0x7FF0000000000000UL,
        0x7FF0000000000000UL
    )]
    [Arguments(
        0x0000000000000001UL,
        0x8000000000000001UL,
        0x8000000000000001UL,
        0x0000000000000001UL,
        0x0000000000000001UL,
        0x0000000000000001UL
    )]
    [Arguments(
        0xFFF8FEDCBA987654UL,
        0xFFF0FEDCBA987654UL,
        0x7FF8123456789ABCUL,
        0x7FF0123456789ABCUL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    [Arguments(
        0x7FF8123456789ABCUL,
        0x7FF0123456789ABCUL,
        0xFFF0FEDCBA987654UL,
        0xFFF8FEDCBA987654UL,
        0x7FF8000000000000UL,
        0x7FF8000000000000UL
    )]
    public async Task 左右のNaNと符号付きゼロと大小のlane_固定仕様の結果bitを返す(
        ulong leftLow64,
        ulong leftHigh64,
        ulong rightLow64,
        ulong rightHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(leftLow64, leftHigh64),
            WasmValue.FromV128(rightLow64, rightHigh64),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F64x2Max, operands);

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
