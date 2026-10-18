using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I16x8GeUTests
{
    [Test]
    [Arguments(
        0x0000FFFF7FFF8000UL,
        0x7FFE800100010001UL,
        0x0000FFFF80007FFFUL,
        0x7FFF800000000002UL,
        0xFFFFFFFF0000FFFFUL,
        0x0000FFFFFFFF0000UL
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
        var actual = SimdHandlerFixture.Execute(Interpreter.I16x8GeU, operands);

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
