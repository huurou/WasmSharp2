using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128XorTests
{
    [Test]
    [Arguments(
        0UL,
        0xFFFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFFUL,
        0UL,
        0xFFFFFFFFFFFFFFFFUL,
        0xFFFFFFFFFFFFFFFFUL
    )]
    [Arguments(
        0x0123456789ABCDEFUL,
        0xFEDCBA9876543210UL,
        0x0F0FF0F05555AAAAUL,
        0xA55A12345678F00FUL,
        0x0E2CB597DCFE6745UL,
        0x5B86A8AC202CC21FUL
    )]
    public async Task ゼロと全bit1と混在bit_上下64bitすべての排他的論理和を返す(
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
        var actual = SimdHandlerFixture.Execute(Interpreter.V128Xor, operands);

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
