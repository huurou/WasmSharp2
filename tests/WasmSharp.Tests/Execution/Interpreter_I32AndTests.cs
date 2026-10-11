using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32AndTests
{
    [Test]
    [Arguments(0, -1, 0)]
    [Arguments(-1, unchecked((int)0x80000000), unchecked((int)0x80000000))]
    [Arguments(unchecked((int)0xAAAAAAAA), unchecked((int)0x55555555), 0)]
    [Arguments(unchecked((int)0x80000001), unchecked((int)0x40000001), 1)]
    public async Task 入力0と全bit1と最上位bitを含む値_同幅の論理積を返す(
        int left,
        int right,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(left), WasmValue.FromI32(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32And, operands);

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
