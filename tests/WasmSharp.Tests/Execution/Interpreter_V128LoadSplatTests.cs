using System.Buffers.Binary;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128LoadSplatTests
{
    [Test]
    [Arguments(1, 1U, 3, 6U, 9, 0xEFEFEFEFEFEFEFEFUL, 0xEFEFEFEFEFEFEFEFUL)]
    [Arguments(1, 1U, 65535, 0U, 65535, 0xEFEFEFEFEFEFEFEFUL, 0xEFEFEFEFEFEFEFEFUL)]
    [Arguments(2, 1U, 3, 6U, 9, 0xCDEFCDEFCDEFCDEFUL, 0xCDEFCDEFCDEFCDEFUL)]
    [Arguments(2, 1U, 65534, 0U, 65534, 0xCDEFCDEFCDEFCDEFUL, 0xCDEFCDEFCDEFCDEFUL)]
    [Arguments(2, 2U, 65535, 0U, 65535, 0xCDEFCDEFCDEFCDEFUL, 0xCDEFCDEFCDEFCDEFUL)]
    [Arguments(4, 1U, 3, 6U, 9, 0x89ABCDEF89ABCDEFUL, 0x89ABCDEF89ABCDEFUL)]
    [Arguments(4, 1U, 65532, 0U, 65532, 0x89ABCDEF89ABCDEFUL, 0x89ABCDEF89ABCDEFUL)]
    [Arguments(4, 2U, 65535, 0U, 65535, 0x89ABCDEF89ABCDEFUL, 0x89ABCDEF89ABCDEFUL)]
    [Arguments(8, 1U, 3, 6U, 9, 0x8123456789ABCDEFUL, 0x8123456789ABCDEFUL)]
    [Arguments(8, 1U, 65528, 0U, 65528, 0x8123456789ABCDEFUL, 0x8123456789ABCDEFUL)]
    [Arguments(8, 2U, 65535, 0U, 65535, 0x8123456789ABCDEFUL, 0x8123456789ABCDEFUL)]
    public async Task 指定幅だけを非整列位置と末尾とページ境界から読む_全laneへ複製して全128bitを積む(
        int byteWidth,
        uint pages,
        int address,
        uint offset,
        int effectiveAddress,
        ulong expectedLow,
        ulong expectedHigh
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, 0x8123456789ABCDEFUL);
        memory.Write((ulong)effectiveAddress, bytes.AsSpan(0, byteWidth));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            (byte)byteWidth,
            offset: offset,
            alignment: byteWidth switch
            {
                8 => 3U,
                4 => 2U,
                2 => 1U,
                _ => 0U,
            }
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128LoadSplat,
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
    [Arguments(1, 65536, 0U)]
    [Arguments(1, -1, 1U)]
    [Arguments(2, 65535, 0U)]
    [Arguments(2, -1, 1U)]
    [Arguments(4, 65533, 0U)]
    [Arguments(4, -1, 1U)]
    [Arguments(8, 65529, 0U)]
    [Arguments(8, -1, 1U)]
    public async Task 指定幅が1byte超過またはアドレス加算で範囲外になる_元の位置を持つtrapを返す(
        int byteWidth,
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            (byte)byteWidth,
            offset: offset
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128LoadSplat,
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
