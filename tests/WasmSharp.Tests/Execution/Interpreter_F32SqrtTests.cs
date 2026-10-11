using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32SqrtTests
{
    [Test]
    [Arguments(0x40800000u, 0x40000000u)]
    [Arguments(0x40000000u, 0x3FB504F3u)]
    [Arguments(0x00000000u, 0x00000000u)]
    [Arguments(0x80000000u, 0x80000000u)]
    [Arguments(0x00000001u, 0x1A3504F3u)]
    [Arguments(0x7F800000u, 0x7F800000u)]
    [Arguments(0xBF800000u, 0x7FC00000u)]
    [Arguments(0xFFC12345u, 0x7FC00000u)]
    [Arguments(0x7F812345u, 0x7FC00000u)]
    public async Task 通常値と境界値とNaN_32bitの平方根結果を返す(
        uint operandBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32Sqrt, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(expectedBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
