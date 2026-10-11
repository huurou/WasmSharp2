using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32ConvertI32STests
{
    [Test]
    [Arguments(0x00000000u, 0x00000000u)]
    [Arguments(0x00000001u, 0x3F800000u)]
    [Arguments(0xFFFFFFFFu, 0xBF800000u)]
    [Arguments(0x0000002Au, 0x42280000u)]
    [Arguments(0x7FFFFFFFu, 0x4F000000u)]
    [Arguments(0x80000000u, 0xCF000000u)]
    [Arguments(0x01000001u, 0x4B800000u)]
    [Arguments(0x01000003u, 0x4B800002u)]
    [Arguments(0xFEFFFFFFu, 0xCB800000u)]
    [Arguments(0xFEFFFFFDu, 0xCB800002u)]
    public async Task 整数の通常値と境界値_符号付き解釈で目的幅へ変換する(
        uint operandBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI32(unchecked((int)operandBits))];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32ConvertI32S, operands);

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
