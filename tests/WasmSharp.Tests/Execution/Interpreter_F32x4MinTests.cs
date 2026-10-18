using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4MinTests
{
    [Test]
    [Arguments(
        0x8000000000000000UL,
        0xC000000040400000UL,
        0x0000000080000000UL,
        0x40400000C0000000UL,
        0x8000000080000000UL,
        0xC0000000C0000000UL
    )]
    [Arguments(
        0x404000007FC12345UL,
        0xBF8000007F812345UL,
        0xFFC5432140400000UL,
        0xFF854321BF800000UL,
        0x7FC000007FC00000UL,
        0x7FC000007FC00000UL
    )]
    [Arguments(
        0xFF8000007F800000UL,
        0x8000000100000001UL,
        0x7F800000FF800000UL,
        0x0000000180000001UL,
        0xFF800000FF800000UL,
        0x8000000180000001UL
    )]
    [Arguments(
        0xFF854321FFC54321UL,
        0x7F8123457FC12345UL,
        0x7F8123457FC12345UL,
        0xFFC54321FF854321UL,
        0x7FC000007FC00000UL,
        0x7FC000007FC00000UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Min, operands);

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
