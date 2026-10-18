using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4MulTests
{
    [Test]
    [Arguments(
        0x800000007FFFFFFFUL,
        0x00000000FFFFFFFFUL,
        0xFFFFFFFF00000002UL,
        0x7FFFFFFFFFFFFFFFUL,
        0x80000000FFFFFFFEUL,
        0x0000000000000001UL
    )]
    [Arguments(
        0x0000000380000000UL,
        0x800000017FFFFFFFUL,
        0xFFFFFFFE00000002UL,
        0x000000037FFFFFFFUL,
        0xFFFFFFFA00000000UL,
        0x8000000300000001UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4Mul, operands);

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
