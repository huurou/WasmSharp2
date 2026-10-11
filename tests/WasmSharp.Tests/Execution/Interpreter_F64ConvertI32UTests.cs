using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64ConvertI32UTests
{
    [Test]
    [Arguments(0x00000000u, 0x0000000000000000UL)]
    [Arguments(0x00000001u, 0x3FF0000000000000UL)]
    [Arguments(0x0000002Au, 0x4045000000000000UL)]
    [Arguments(0x7FFFFFFFu, 0x41DFFFFFFFC00000UL)]
    [Arguments(0x80000000u, 0x41E0000000000000UL)]
    [Arguments(0xFFFFFFFFu, 0x41EFFFFFFFE00000UL)]
    [Arguments(0x80000001u, 0x41E0000000200000UL)]
    public async Task 整数の通常値と境界値_符号なし解釈で目的幅へ変換する(
        uint operandBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(unchecked((int)operandBits))];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64ConvertI32U, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(actual.Value.AsF64Bits()).IsEqualTo(expectedBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
