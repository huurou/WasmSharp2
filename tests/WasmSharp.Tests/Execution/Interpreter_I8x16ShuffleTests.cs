using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I8x16ShuffleTests
{
    [Test]
    [Arguments(false, 0x888708078F800F00UL, 0x07088788000F808FUL)]
    [Arguments(true, 0x080788870F008F80UL, 0x87880708808F000FUL)]
    public async Task 両operandの端と中間のbyteを指定_第1vectorから順に連結して選択する(
        bool reverseOperands,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        var first = WasmValue.FromV128(0x0706050403020100, 0x0F0E0D0C0B0A0908);
        var second = WasmValue.FromV128(0x8786858483828180, 0x8F8E8D8C8B8A8988);
        WasmValue[] operands = reverseOperands ? [second, first] : [first, second];

        // Act
        var actual = SimdHandlerFixture.Execute(
            Interpreter.I8x16Shuffle,
            operands,
            shuffleLanes: [0, 15, 16, 31, 7, 8, 23, 24, 31, 16, 15, 0, 24, 23, 8, 7]
        );

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
