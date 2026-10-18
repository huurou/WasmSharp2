using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I8x16SwizzleTests
{
    [Test]
    [Arguments(
        0x010E080700FF100FUL,
        0x07010E0008FF0F10UL,
        0x818E88878000008FUL,
        0x87818E8088008F00UL
    )]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0x1010101010101010UL, 0UL, 0UL)]
    public async Task indexに15と16と255を含む_範囲内のbyteを選択し範囲外をゼロにする(
        ulong indexLow64,
        ulong indexHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        WasmValue[] operands =
        [
            WasmValue.FromV128(0x8786858483828180, 0x8F8E8D8C8B8A8988),
            WasmValue.FromV128(indexLow64, indexHigh64),
        ];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I8x16Swizzle, operands);

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
