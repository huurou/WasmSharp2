using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F64PromoteF32Tests
{
    [Test]
    [Arguments(0x3FC00000u, 0x3FF8000000000000UL)]
    [Arguments(0xBFC00000u, 0xBFF8000000000000UL)]
    [Arguments(0x3F800001u, 0x3FF0000020000000UL)]
    [Arguments(0x3F800003u, 0x3FF0000060000000UL)]
    [Arguments(0x00000000u, 0x0000000000000000UL)]
    [Arguments(0x80000000u, 0x8000000000000000UL)]
    [Arguments(0x7F800000u, 0x7FF0000000000000UL)]
    [Arguments(0xFF800000u, 0xFFF0000000000000UL)]
    [Arguments(0x00000001u, 0x36A0000000000000UL)]
    [Arguments(0x80000001u, 0xB6A0000000000000UL)]
    [Arguments(0x007FFFFFu, 0x380FFFFFC0000000UL)]
    [Arguments(0x00800000u, 0x3810000000000000UL)]
    [Arguments(0x7F7FFFFFu, 0x47EFFFFFE0000000UL)]
    [Arguments(0xFF7FFFFFu, 0xC7EFFFFFE0000000UL)]
    public async Task 非NaNの通常値と境界値_値を正確に保持してf64へ拡張する(
        uint operandBits,
        ulong expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64PromoteF32, operands);

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

    [Test]
    [Arguments(0x7FC00000u)]
    [Arguments(0xFFC00000u)]
    [Arguments(0xFFC12345u)]
    [Arguments(0x7F812345u)]
    [Arguments(0xFF812345u)]
    public async Task canonicalとpayload付きのquietおよびsignalingNaN_正のcanonicalNaNへ揃える(
        uint operandBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F64PromoteF32, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F64);
            await Assert.That(actual.Value.AsF64Bits()).IsEqualTo(0x7FF8000000000000UL);
            await Assert.That(double.IsNaN(actual.Value.AsF64())).IsTrue();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
