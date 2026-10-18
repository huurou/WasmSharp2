using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64x2BitmaskTests
{
    [Test]
    [Arguments(0x0000000000000000UL, 0x0000000000000000UL, 0)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL, 3)]
    [Arguments(0x0000000000000001UL, 0x0000000000000001UL, 0)]
    [Arguments(0x8000000000000000UL, 0x8000000000000000UL, 3)]
    [Arguments(0x8000000000000000UL, 0x0000000000000001UL, 1)]
    [Arguments(0x0000000000000001UL, 0x8000000000000000UL, 2)]
    public async Task 符号bitと下位bitが混在するlane_最上位bitをlane番号順に集める(
        ulong low64,
        ulong high64,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I64x2Bitmask, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
