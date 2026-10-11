using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32ConvertI64STests
{
    [Test]
    [Arguments(0x0000000000000000UL, 0x00000000u)]
    [Arguments(0x0000000000000001UL, 0x3F800000u)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0xBF800000u)]
    [Arguments(0x000000000000002AUL, 0x42280000u)]
    [Arguments(0x7FFFFFFFFFFFFFFFUL, 0x5F000000u)]
    [Arguments(0x8000000000000000UL, 0xDF000000u)]
    [Arguments(0x0000000001000001UL, 0x4B800000u)]
    [Arguments(0x0000000001000003UL, 0x4B800002u)]
    [Arguments(0xFFFFFFFFFEFFFFFFUL, 0xCB800000u)]
    [Arguments(0xFFFFFFFFFEFFFFFDUL, 0xCB800002u)]
    [Arguments(0x7FFFFF4000000001UL, 0x5EFFFFFFu)]
    [Arguments(0x8000004000000001UL, 0xDEFFFFFFu)]
    [Arguments(0x0020000020000001UL, 0x5A000001u)]
    [Arguments(0xFFDFFFFFDFFFFFFFUL, 0xDA000001u)]
    public async Task 整数の通常値と境界値_符号付き解釈で目的幅へ変換する(
        ulong operandBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromI64(unchecked((long)operandBits))];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32ConvertI64S, operands);

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
