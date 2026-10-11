using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64AndTests
{
    [Test]
    [Arguments(0L, -1L, 0L)]
    [Arguments(-1L, unchecked((long)0x8000000000000000UL), unchecked((long)0x8000000000000000UL))]
    [Arguments(unchecked((long)0xAAAAAAAAAAAAAAAAUL), unchecked((long)0x5555555555555555UL), 0L)]
    [Arguments(unchecked((long)0x8000000000000001UL), unchecked((long)0x4000000000000001UL), 1L)]
    public async Task 入力0と全bit1と最上位bitを含む値_同幅の論理積を返す(
        long left,
        long right,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(left), WasmValue.FromI64(right)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64And, operands);

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
