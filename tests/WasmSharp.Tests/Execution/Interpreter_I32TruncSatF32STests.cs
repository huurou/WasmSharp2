using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I32TruncSatF32STests
{
    [Test]
    [Arguments(0x40FCCCCDU, 7)]
    [Arguments(0x00000000U, 0)]
    [Arguments(0x80000000U, 0)]
    [Arguments(0xBF666666U, 0)]
    [Arguments(0xBF7FFFFFU, 0)]
    [Arguments(0x00000001U, 0)]
    [Arguments(0x80000001U, 0)]
    [Arguments(0xC0FCCCCDU, -7)]
    [Arguments(0x4EFFFFFFU, 2147483520)]
    [Arguments(0xCF000000U, int.MinValue)]
    [Arguments(0xCEFFFFFFU, -2147483520)]
    [Arguments(0x4F000000U, int.MaxValue)]
    [Arguments(0x4F000001U, int.MaxValue)]
    [Arguments(0xCF000001U, int.MinValue)]
    [Arguments(0x7F800000U, int.MaxValue)]
    [Arguments(0xFF800000U, int.MinValue)]
    [Arguments(0x7FC00000U, 0)]
    [Arguments(0xFFC12345U, 0)]
    [Arguments(0x7F812345U, 0)]
    public async Task 入力が有限値または特殊値_切捨てと飽和した整数をtrapなしで返す(
        uint operandBits,
        int expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF32Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I32TruncSatF32S, operands);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Result.TrapReason).IsNull();
            await Assert.That(actual.Result.FunctionIndex).IsNull();
            await Assert.That(actual.Result.ByteOffset).IsNull();
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI32(expected));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }
}
