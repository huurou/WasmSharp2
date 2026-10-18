using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32x4BitmaskTests
{
    [Test]
    [Arguments(0x0000000000000000UL, 0x0000000000000000UL, 0)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL, 15)]
    [Arguments(0x0000000100000001UL, 0x0000000100000001UL, 0)]
    [Arguments(0x8000000080000000UL, 0x8000000080000000UL, 15)]
    [Arguments(0x0000000180000000UL, 0x0000000180000000UL, 5)]
    [Arguments(0x0000000100000001UL, 0x8000000000000001UL, 8)]
    public async Task 符号bitと下位bitが混在するlane_最上位bitをlane番号順に集める(
        ulong low64,
        ulong high64,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromV128(low64, high64)];

        // Act
        var actual = SimdHandlerFixture.Execute(Interpreter.I32x4Bitmask, operands);

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
