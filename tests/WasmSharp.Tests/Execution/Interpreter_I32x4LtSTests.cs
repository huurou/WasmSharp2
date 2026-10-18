using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4LtSTests
{
    [Test]
    [Arguments(
        0x7FFFFFFF80000000UL,
        0x00000001FFFFFFFFUL,
        0x800000007FFFFFFFUL,
        0x00000000FFFFFFFFUL,
        0x00000000FFFFFFFFUL,
        0x0000000000000000UL
    )]
    [Arguments(
        0xFFFFFFFF00000000UL,
        0x8000000100000001UL,
        0x0000000000000000UL,
        0x8000000000000002UL,
        0xFFFFFFFF00000000UL,
        0x00000000FFFFFFFFUL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4LtS, operands);

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
