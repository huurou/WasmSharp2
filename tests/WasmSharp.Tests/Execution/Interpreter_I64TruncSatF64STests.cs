using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_I64TruncSatF64STests
{
    [Test]
    [Arguments(0x401F99999999999AUL, 7L)]
    [Arguments(0x0000000000000000UL, 0L)]
    [Arguments(0x8000000000000000UL, 0L)]
    [Arguments(0xBFECCCCCCCCCCCCDUL, 0L)]
    [Arguments(0xBFEFFFFFFFFFFFFFUL, 0L)]
    [Arguments(0x0000000000000001UL, 0L)]
    [Arguments(0x8000000000000001UL, 0L)]
    [Arguments(0xC01F99999999999AUL, -7L)]
    [Arguments(0x43DFFFFFFFFFFFFFUL, 9223372036854774784L)]
    [Arguments(0xC3E0000000000000UL, long.MinValue)]
    [Arguments(0xC3DFFFFFFFFFFFFFUL, -9223372036854774784L)]
    [Arguments(0x43E0000000000000UL, long.MaxValue)]
    [Arguments(0x43E0000000000001UL, long.MaxValue)]
    [Arguments(0xC3E0000000000001UL, long.MinValue)]
    [Arguments(0x7FF0000000000000UL, long.MaxValue)]
    [Arguments(0xFFF0000000000000UL, long.MinValue)]
    [Arguments(0x7FF8000000000000UL, 0L)]
    [Arguments(0xFFF8123456789ABCUL, 0L)]
    [Arguments(0x7FF0123456789ABCUL, 0L)]
    public async Task 入力が有限値または特殊値_切捨てと飽和した整数をtrapなしで返す(
        ulong operandBits,
        long expected
    )
    {
        // Arrange
        WasmValue[] operands = [WasmValue.FromF64Bits(operandBits)];

        // Act
        var actual = NumericHandlerFixture.Execute(Interpreter.I64TruncSatF64S, operands);

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
