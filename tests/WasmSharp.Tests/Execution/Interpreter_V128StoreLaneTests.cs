using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128StoreLaneTests
{
    [Test]
    [Arguments(1, 0, 1U, 3, 6U, 9)]
    [Arguments(1, 15, 1U, 65535, 0U, 65535)]
    [Arguments(2, 0, 1U, 3, 6U, 9)]
    [Arguments(2, 7, 1U, 65534, 0U, 65534)]
    [Arguments(2, 7, 2U, 65535, 0U, 65535)]
    [Arguments(4, 0, 1U, 3, 6U, 9)]
    [Arguments(4, 3, 1U, 65532, 0U, 65532)]
    [Arguments(4, 3, 2U, 65535, 0U, 65535)]
    [Arguments(8, 0, 1U, 3, 6U, 9)]
    [Arguments(8, 1, 1U, 65528, 0U, 65528)]
    [Arguments(8, 1, 2U, 65535, 0U, 65535)]
    public async Task 各幅の先頭末尾laneを非整列位置とメモリ末尾とページ境界へ書く_対象範囲だけを変更し結果値を積まない(
        int byteWidth,
        int laneIndex,
        uint pages,
        int address,
        uint offset,
        int effectiveAddress
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        var expected = new byte[pages * 65536];
        Array.Fill(expected, (byte)0xA5);
        memory.Write(0, expected);
        byte[] vectorBytes =
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
        ];
        vectorBytes
            .AsSpan(laneIndex * byteWidth, byteWidth)
            .CopyTo(expected.AsSpan(effectiveAddress));
        var instruction = new Instruction(
            default,
            default,
            12345678901,
            0,
            memoryArgument: new(
                byteWidth switch
                {
                    8 => 3U,
                    4 => 2U,
                    2 => 1U,
                    _ => 0U,
                },
                offset
            ),
            memoryAccessSignature: new(WasmValueKind.V128, (byte)byteWidth, false),
            laneIndex: (byte)laneIndex
        );
        var after = new byte[expected.Length];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128StoreLane,
            memory,
            in instruction,
            [
                WasmValue.FromI32(address),
                WasmValue.FromV128(0xFEDCBA9876543210UL, 0x0123456789ABCDEFUL),
            ]
        );
        memory.Read(0, after);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(after.SequenceEqual(expected)).IsTrue();
            await Assert.That(memory.PageCount).IsEqualTo(pages);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(1, 0, 65536, 0U)]
    [Arguments(1, 15, 65536, 0U)]
    [Arguments(1, 15, -1, 1U)]
    [Arguments(1, 0, 1, uint.MaxValue)]
    [Arguments(2, 0, 65535, 0U)]
    [Arguments(2, 7, 65535, 0U)]
    [Arguments(2, 7, -1, 1U)]
    [Arguments(2, 0, 1, uint.MaxValue)]
    [Arguments(4, 0, 65533, 0U)]
    [Arguments(4, 3, 65533, 0U)]
    [Arguments(4, 3, -1, 1U)]
    [Arguments(4, 0, 1, uint.MaxValue)]
    [Arguments(8, 0, 65529, 0U)]
    [Arguments(8, 1, 65529, 0U)]
    [Arguments(8, 1, -1, 1U)]
    [Arguments(8, 0, 1, uint.MaxValue)]
    public async Task 各幅の先頭末尾laneで1byte超過または33bit加算で範囲外になる_全メモリを変更せず元の位置を持つtrapを返す(
        int byteWidth,
        int laneIndex,
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var before = new byte[65536];
        Array.Fill(before, (byte)0xA5);
        memory.Write(0, before);
        var instruction = new Instruction(
            default,
            default,
            12345678901,
            0,
            memoryArgument: new(0, offset),
            memoryAccessSignature: new(WasmValueKind.V128, (byte)byteWidth, false),
            laneIndex: (byte)laneIndex
        );
        var after = new byte[before.Length];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128StoreLane,
            memory,
            in instruction,
            [
                WasmValue.FromI32(address),
                WasmValue.FromV128(0xFEDCBA9876543210UL, 0x0123456789ABCDEFUL),
            ]
        );
        memory.Read(0, after);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(actual.Result.TrapReason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(actual.Result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(actual.Result.ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(actual.Result.Values).IsEmpty();
            await Assert.That(after.SequenceEqual(before)).IsTrue();
            await Assert.That(memory.PageCount).IsEqualTo(1U);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }
}
