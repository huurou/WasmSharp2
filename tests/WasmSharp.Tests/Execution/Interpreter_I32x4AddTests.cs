using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4AddTests
{
    [Test]
    [Arguments(
        0x800000007FFFFFFFUL,
        0x00000000FFFFFFFFUL,
        0xFFFFFFFF00000001UL,
        0xFFFFFFFF00000001UL,
        0x7FFFFFFF80000000UL,
        0xFFFFFFFF00000000UL
    )]
    [Arguments(
        0x8000000000000001UL,
        0xFFFFFFFE7FFFFFFFUL,
        0x8000000000000002UL,
        0xFFFFFFFD7FFFFFFFUL,
        0x0000000000000003UL,
        0xFFFFFFFBFFFFFFFEUL
    )]
    public async Task 正負の端点を含む上下のlane_演算結果を幅内へ折り返す(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4Add, operands);

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
