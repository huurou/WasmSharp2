using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64ConvertI64STests
{
    [Test]
    [Arguments(0x0000000000000000UL, 0x0000000000000000UL)]
    [Arguments(0x0000000000000001UL, 0x3FF0000000000000UL)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0xBFF0000000000000UL)]
    [Arguments(0x000000000000002AUL, 0x4045000000000000UL)]
    [Arguments(0x7FFFFFFFFFFFFFFFUL, 0x43E0000000000000UL)]
    [Arguments(0x8000000000000000UL, 0xC3E0000000000000UL)]
    [Arguments(0x001FFFFFFFFFFFFFUL, 0x433FFFFFFFFFFFFFUL)]
    [Arguments(0x0020000000000001UL, 0x4340000000000000UL)]
    [Arguments(0x0020000000000003UL, 0x4340000000000002UL)]
    [Arguments(0xFFDFFFFFFFFFFFFFUL, 0xC340000000000000UL)]
    [Arguments(0xFFDFFFFFFFFFFFFDUL, 0xC340000000000002UL)]
    public async Task 整数の通常値と境界値_符号付き解釈で目的幅へ変換する(
        ulong operandBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(unchecked((long)operandBits))];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64ConvertI64S, operands);

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
