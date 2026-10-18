using System.Buffers.Binary;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128Load32x2UTests
{
    [Test]
    [Arguments(1U, 3, 6U, 9, 0x7FFFFFFF80000000UL, 0x0000000080000000UL, 0x000000007FFFFFFFUL)]
    [Arguments(
        1U,
        65528,
        0U,
        65528,
        0x7FFFFFFF80000000UL,
        0x0000000080000000UL,
        0x000000007FFFFFFFUL
    )]
    [Arguments(
        2U,
        65533,
        0U,
        65533,
        0x7FFFFFFF80000000UL,
        0x0000000080000000UL,
        0x000000007FFFFFFFUL
    )]
    [Arguments(1U, 1, 0U, 1, 0x00000000FFFFFFFFUL, 0x00000000FFFFFFFFUL, 0UL)]
    public async Task 符号境界を含む8byteを非整列位置と末尾とページ境界から読む_各laneを拡張して全128bitを積む(
        uint pages,
        int address,
        uint offset,
        int effectiveAddress,
        ulong input,
        ulong expectedLow,
        ulong expectedHigh
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, input);
        memory.Write((ulong)effectiveAddress, bytes);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            8,
            offset: offset,
            alignment: 3
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128Load32x2U,
            memory,
            in instruction,
            [WasmValue.FromI32(address)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.V128);
            await Assert.That(actual.Value.AsV128()).IsEqualTo((expectedLow, expectedHigh));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(65529, 0U)]
    [Arguments(-1, 1U)]
    public async Task 指定8byteが1byte超過またはアドレス加算で範囲外になる_元の位置を持つtrapを返す(
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            8,
            offset: offset
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128Load32x2U,
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
