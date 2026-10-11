using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32ShlTests
{
    [Test]
    [Arguments(unchecked((int)0x80000001), 0, unchecked((int)0x80000001))]
    [Arguments(unchecked((int)0x80000001), 32, unchecked((int)0x80000001))]
    [Arguments(unchecked((int)0x80000001), 33, 2)]
    [Arguments(unchecked((int)0x80000001), -1, unchecked((int)0x80000000))]
    [Arguments(unchecked((int)0x80000000), 1, 0)]
    [Arguments(-1, 1, -2)]
    public async Task 幅境界と負の移動量を含む値_幅で剰余化して左シフトする(
        int left,
        int right,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(left), WasmValue.FromI32(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32Shl, operands);

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
