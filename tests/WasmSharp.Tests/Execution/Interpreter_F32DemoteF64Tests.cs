using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_F32DemoteF64Tests
{
    [Test]
    [Arguments(0x3FF8000000000000UL, 0x3FC00000u)]
    [Arguments(0xBFF8000000000000UL, 0xBFC00000u)]
    [Arguments(0x3FF0000010000000UL, 0x3F800000u)]
    [Arguments(0x3FF0000030000000UL, 0x3F800002u)]
    [Arguments(0xBFF0000010000000UL, 0xBF800000u)]
    [Arguments(0xBFF0000030000000UL, 0xBF800002u)]
    [Arguments(0x3FF000000FFFFFFFUL, 0x3F800000u)]
    [Arguments(0x3FF0000010000001UL, 0x3F800001u)]
    [Arguments(0x0000000000000000UL, 0x00000000u)]
    [Arguments(0x8000000000000000UL, 0x80000000u)]
    [Arguments(0x7FF0000000000000UL, 0x7F800000u)]
    [Arguments(0xFFF0000000000000UL, 0xFF800000u)]
    [Arguments(0x36A0000000000000UL, 0x00000001u)]
    [Arguments(0x3810000000000000UL, 0x00800000u)]
    [Arguments(0x3690000000000000UL, 0x00000000u)]
    [Arguments(0xB690000000000000UL, 0x80000000u)]
    [Arguments(0x36A8000000000000UL, 0x00000002u)]
    [Arguments(0x0000000000000001UL, 0x00000000u)]
    [Arguments(0x8000000000000001UL, 0x80000000u)]
    [Arguments(0x47EFFFFFE0000000UL, 0x7F7FFFFFu)]
    [Arguments(0x47EFFFFFEFFFFFFFUL, 0x7F7FFFFFu)]
    [Arguments(0x47EFFFFFF0000000UL, 0x7F800000u)]
    [Arguments(0xC7EFFFFFF0000000UL, 0xFF800000u)]
    [Arguments(0x7FEFFFFFFFFFFFFFUL, 0x7F800000u)]
    [Arguments(0xFFEFFFFFFFFFFFFFUL, 0xFF800000u)]
    public async Task 非NaNの通常値と境界値_最近接偶数で丸めて符号付き0と無限大を保持する(
        ulong operandBits,
        uint expectedBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32DemoteF64, operands);

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

    [Test]
    [Arguments(0x7FF8000000000000UL)]
    [Arguments(0xFFF8000000000000UL)]
    [Arguments(0xFFF8123456789ABCUL)]
    [Arguments(0x7FF0123456789ABCUL)]
    [Arguments(0xFFF0123456789ABCUL)]
    public async Task Canonicalとpayload付きのquietおよびsignalingNaN_正のcanonicalNaNへ揃える(
        ulong operandBits
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.F32DemoteF64, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.F32);
            await Assert.That(actual.Value.AsF32Bits()).IsEqualTo(0x7FC00000u);
            await Assert.That(float.IsNaN(actual.Value.AsF32())).IsTrue();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
