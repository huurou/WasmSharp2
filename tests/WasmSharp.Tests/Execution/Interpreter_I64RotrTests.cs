using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64RotrTests
{
    [Test]
    [Arguments(unchecked((long)0x8000000000000001UL), 0L, unchecked((long)0x8000000000000001UL))]
    [Arguments(unchecked((long)0x8000000000000001UL), 64L, unchecked((long)0x8000000000000001UL))]
    [Arguments(unchecked((long)0x8000000000000001UL), 65L, unchecked((long)0xC000000000000000UL))]
    [Arguments(unchecked((long)0x8000000000000001UL), -1L, 3L)]
    [Arguments(
        unchecked((long)0x8000000000000000UL),
        4294967297L,
        unchecked((long)0x4000000000000000UL)
    )]
    [Arguments(-1L, 17L, -1L)]
    [Arguments(
        unchecked((long)0x8000000000000001UL),
        unchecked((long)0x8000000000000000UL),
        unchecked((long)0x8000000000000001UL)
    )]
    public async Task 幅境界と負の移動量を含む値_幅で剰余化して右回転する(
        long left,
        long right,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(left), WasmValue.FromI64(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64Rotr, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI64(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
