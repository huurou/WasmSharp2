using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128LoadTests
{
    [Test]
    [Arguments(1U, 3, 6U, 9)]
    [Arguments(1U, 65520, 0U, 65520)]
    [Arguments(2U, 65528, 0U, 65528)]
    public async Task 非整列位置とメモリ末尾とページ境界から読み出す_全128bitをリトルエンディアンで積む(
        uint pages,
        int address,
        uint offset,
        int effectiveAddress
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        memory.Write(
            (ulong)effectiveAddress,
            [
                0x10,
                0x32,
                0x54,
                0x76,
                0x98,
                0xBA,
                0xDC,
                0xFE,
                0xEF,
                0xCD,
                0xAB,
                0x89,
                0x67,
                0x45,
                0x23,
                0x01,
            ]
        );
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            16,
            offset: offset,
            alignment: 4
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128Load,
            memory,
            in instruction,
            [WasmValue.FromI32(address)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert
                .That(actual.Value.AsV128())
                .IsEqualTo((0xFEDCBA9876543210UL, 0x0123456789ABCDEFUL));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0U, 0, 0U)]
    [Arguments(1U, 65521, 0U)]
    [Arguments(1U, 65536, 0U)]
    [Arguments(1U, -1, 0U)]
    [Arguments(1U, -1, 1U)]
    [Arguments(1U, 1, uint.MaxValue)]
    [Arguments(1U, int.MinValue, 0x80000000U)]
    [Arguments(1U, -1, uint.MaxValue)]
    public async Task 読み出し範囲がメモリ外または33bit加算で範囲外になる_折り返さず元の位置を持つtrapを返す(
        uint pages,
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            16,
            offset: offset
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128Load,
            memory,
            in instruction,
            [WasmValue.FromI32(address)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(actual.Result.TrapReason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }
}
