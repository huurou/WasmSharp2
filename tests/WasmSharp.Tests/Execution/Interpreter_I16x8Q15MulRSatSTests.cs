using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8Q15MulRSatSTests
{
    [Test]
    [Arguments(
        0x000100017FFF8000UL,
        0xFFFFFFFFFFFF0001UL,
        0x40003FFF7FFF8000UL,
        0x400140003FFF4001UL,
        0x000100007FFE7FFFUL,
        0xFFFF000000000001UL
    )]
    [Arguments(
        0x800080007FFF8000UL,
        0x40010000C0008001UL,
        0x0001800180007FFFUL,
        0xC000800040000001UL,
        0xFFFF7FFF80018001UL,
        0xE0000000E000FFFFUL
    )]
    public async Task 丸めと飽和の境界を含む非対称lane_符号付き積を丸めて16bitへ飽和する(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8Q15MulRSatS, operands);

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
