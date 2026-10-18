using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2EqTests
{
    [Test]
    [Arguments(
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL,
        0x7FFFFFFFFFFFFFFFUL,
        0x8000000000000000UL,
        0x0000000000000000UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000001UL,
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL,
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x0000000000000000UL,
        0x8000000000000001UL,
        0x0000000000000000UL,
        0x8000000000000000UL,
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL
    )]
    public async Task 符号境界と大小と等値が混在するlane_全bit1か0をlane順に返す(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2Eq, operands);

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
