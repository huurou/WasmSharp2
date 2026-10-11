using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32RotrTests
{
    [Test]
    [Arguments(unchecked((int)0x80000001), 0, unchecked((int)0x80000001))]
    [Arguments(unchecked((int)0x80000001), 32, unchecked((int)0x80000001))]
    [Arguments(unchecked((int)0x80000001), 33, unchecked((int)0xC0000000))]
    [Arguments(unchecked((int)0x80000001), -1, 3)]
    [Arguments(unchecked((int)0x80000000), 1, unchecked((int)0x40000000))]
    [Arguments(-1, 17, -1)]
    public async Task 幅境界と負の移動量を含む値_幅で剰余化して右回転する(
        int left,
        int right,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(left), WasmValue.FromI32(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32Rotr, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI32(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
