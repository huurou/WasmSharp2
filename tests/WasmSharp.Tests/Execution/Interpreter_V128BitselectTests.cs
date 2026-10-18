using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128BitselectTests
{
    [Test]
    [Arguments(false, 0UL, 0UL, 0x0F0FF0F05555AAAAUL, 0xA55A12345678F00FUL)]
    [Arguments(
        false,
        0xFFFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFFUL,
        0x0123456789ABCDEFUL,
        0xFEDCBA9876543210UL
    )]
    [Arguments(
        false,
        0xA5A55A5AF00F0FF0UL,
        0x81FF007E55AA33CCUL,
        0x0B2BE0E2855BADEAUL,
        0xA4DC12185650F203UL
    )]
    [Arguments(
        true,
        0xA5A55A5AF00F0FF0UL,
        0x81FF007E55AA33CCUL,
        0x0507557559A5CAAFUL,
        0xFF5ABAB4767C301CUL
    )]
    public async Task ゼロと全bit1とbit単位のmask_1のbitは第1operandから選択する(
        bool reverseOperands,
        ulong maskLow64,
        ulong maskHigh64,
        ulong expectedLow64,
        ulong expectedHigh64
    )
    {
        // Arrange
        var first = WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210);
        var second = WasmValue.FromV128(0x0F0FF0F05555AAAA, 0xA55A12345678F00F);
        var mask = WasmValue.FromV128(maskLow64, maskHigh64);
        WasmValue[] operands = reverseOperands ? [second, first, mask] : [first, second, mask];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.V128Bitselect, operands);

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
