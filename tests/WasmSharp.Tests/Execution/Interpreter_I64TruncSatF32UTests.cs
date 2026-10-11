using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64TruncSatF32UTests
{
    [Test]
    [Arguments(0x40FCCCCDU, 7L)]
    [Arguments(0x00000000U, 0L)]
    [Arguments(0x80000000U, 0L)]
    [Arguments(0xBF666666U, 0L)]
    [Arguments(0xBF7FFFFFU, 0L)]
    [Arguments(0x00000001U, 0L)]
    [Arguments(0x80000001U, 0L)]
    [Arguments(0x5F000000U, long.MinValue)]
    [Arguments(0x5F7FFFFFU, -1099511627776L)]
    [Arguments(0x5F800000U, -1L)]
    [Arguments(0x5F800001U, -1L)]
    [Arguments(0xBF800000U, 0L)]
    [Arguments(0xBF800001U, 0L)]
    [Arguments(0xC0FCCCCDU, 0L)]
    [Arguments(0x7F800000U, -1L)]
    [Arguments(0xFF800000U, 0L)]
    [Arguments(0x7FC00000U, 0L)]
    [Arguments(0xFFC12345U, 0L)]
    [Arguments(0x7F812345U, 0L)]
    public async Task 入力が有限値または特殊値_切捨てと飽和した整数をtrapなしで返す(
        uint operandBits,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64TruncSatF32U, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Result.TrapReason).IsNull();
            await Assert.That(actual.Result.FunctionIndex).IsNull();
            await Assert.That(actual.Result.ByteOffset).IsNull();
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI64(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
