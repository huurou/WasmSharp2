using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32x4TruncTests
{
    [Test]
    [Arguments(
        0xBF0000003F000000UL,
        0xBFC000003FC00000UL,
        0x8000000000000000UL,
        0xBF8000003F800000UL
    )]
    [Arguments(
        0xC020000040200000UL,
        0xC060000040600000UL,
        0xC000000040000000UL,
        0xC040000040400000UL
    )]
    [Arguments(
        0x8000000100000001UL,
        0x8000000000000000UL,
        0x8000000000000000UL,
        0x8000000000000000UL
    )]
    [Arguments(
        0xFF8000007F800000UL,
        0x3FA000007F812345UL,
        0xFF8000007F800000UL,
        0x3F8000007FC00000UL
    )]
    [Arguments(
        0xFFC543217FC12345UL,
        0xBFA00000FF854321UL,
        0x7FC000007FC00000UL,
        0xBF8000007FC00000UL
    )]
    public async Task 正負の丸め境界と特殊値のlane_符号付きゼロを保ち整数値化する(
        ulong inputLow64,
        ulong inputHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(inputLow64, inputHigh64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.F32x4Trunc, operands);

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
