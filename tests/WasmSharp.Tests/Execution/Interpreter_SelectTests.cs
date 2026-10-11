using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_SelectTests
{
    [Test]
    [Arguments(WasmValueKind.I32, 0xFEDCBA98UL, 0x12345678UL, 0)]
    [Arguments(WasmValueKind.I32, 0xFEDCBA98UL, 0x12345678UL, 1)]
    [Arguments(WasmValueKind.I32, 0xFEDCBA98UL, 0x12345678UL, -1)]
    [Arguments(WasmValueKind.I64, 0xFEDCBA9876543210UL, 0x123456789ABCDEF0UL, 0)]
    [Arguments(WasmValueKind.I64, 0xFEDCBA9876543210UL, 0x123456789ABCDEF0UL, 1)]
    [Arguments(WasmValueKind.I64, 0xFEDCBA9876543210UL, 0x123456789ABCDEF0UL, -1)]
    [Arguments(WasmValueKind.F32, 0xFF812345UL, 0x7FC54321UL, 0)]
    [Arguments(WasmValueKind.F32, 0xFF812345UL, 0x7FC54321UL, 1)]
    [Arguments(WasmValueKind.F32, 0xFF812345UL, 0x7FC54321UL, -1)]
    [Arguments(WasmValueKind.F32, 0x80000000UL, 0x00000000UL, 0)]
    [Arguments(WasmValueKind.F32, 0x80000000UL, 0x00000000UL, 1)]
    [Arguments(WasmValueKind.F64, 0xFFF0123456789ABCUL, 0x7FF8ABCDEF012345UL, 0)]
    [Arguments(WasmValueKind.F64, 0xFFF0123456789ABCUL, 0x7FF8ABCDEF012345UL, 1)]
    [Arguments(WasmValueKind.F64, 0xFFF0123456789ABCUL, 0x7FF8ABCDEF012345UL, -1)]
    [Arguments(WasmValueKind.F64, 0x8000000000000000UL, 0x0000000000000000UL, 0)]
    [Arguments(WasmValueKind.F64, 0x8000000000000000UL, 0x0000000000000000UL, 1)]
    public async Task スカラー候補と条件を積んでいる_両候補と条件を消費して選択値の型と生bitを保つ(
        WasmValueKind kind,
        ulong firstBits,
        ulong secondBits,
        int condition
    )
    {
        // Arrange
        var first = CreateValue(kind, firstBits);
        var second = CreateValue(kind, secondBits);
        var expected = condition == 0 ? second : first;
        WasmValue[] operands = [first, second, WasmValue.FromI32(condition)];
        var instruction = new Instruction(default, default, 81, 0);

        // Act
        var actual = ControlHandlerFixture.Execute(Interpreter.Select, in instruction, operands);
        var selected = actual.Values[^1];
        var selectedBits = kind switch
        {
            WasmValueKind.I32 => unchecked((uint)selected.AsI32()),
            WasmValueKind.I64 => unchecked((ulong)selected.AsI64()),
            WasmValueKind.F32 => selected.AsF32Bits(),
            _ => selected.AsF64Bits(),
        };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.After).IsEqualTo(actual.Before);
            await Assert.That(actual.FrameCount).IsEqualTo(1);
            await Assert.That(actual.CallDepth).IsEqualTo(1);
            await Assert.That(actual.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(actual.Values.Length).IsEqualTo(3);
            await Assert.That(actual.Values[0]).IsEqualTo(ControlHandlerFixture.Local);
            await Assert.That(actual.Values[1]).IsEqualTo(ControlHandlerFixture.Sentinel);
            await Assert.That(selected.Kind).IsEqualTo(kind);
            await Assert.That(selected).IsEqualTo(expected);
            await Assert.That(selectedBits).IsEqualTo(condition == 0 ? secondBits : firstBits);
        }
    }

    private static WasmValue CreateValue(WasmValueKind kind, ulong bits)
    {
        return kind switch
        {
            WasmValueKind.I32 => WasmValue.FromI32(unchecked((int)bits)),
            WasmValueKind.I64 => WasmValue.FromI64(unchecked((long)bits)),
            WasmValueKind.F32 => WasmValue.FromF32Bits((uint)bits),
            _ => WasmValue.FromF64Bits(bits),
        };
    }
}
