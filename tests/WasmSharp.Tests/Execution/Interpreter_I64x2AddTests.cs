using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2AddTests
{
    [Test]
    [Arguments(
        0x7FFFFFFFFFFFFFFFUL,
        0x8000000000000000UL,
        0x0000000000000001UL,
        0xFFFFFFFFFFFFFFFFUL,
        0x8000000000000000UL,
        0x7FFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL,
        0x0000000000000001UL,
        0xFFFFFFFFFFFFFFFFUL,
        0x0000000000000000UL,
        0xFFFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x0000000000000001UL,
        0x8000000000000000UL,
        0x0000000000000002UL,
        0x8000000000000000UL,
        0x0000000000000003UL,
        0x0000000000000000UL
    )]
    [Arguments(
        0x7FFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFEUL,
        0x7FFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFDUL,
        0xFFFFFFFFFFFFFFFEUL,
        0xFFFFFFFFFFFFFFFBUL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2Add, operands);

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
