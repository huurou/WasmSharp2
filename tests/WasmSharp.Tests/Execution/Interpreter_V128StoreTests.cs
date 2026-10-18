using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_V128StoreTests
{
    [Test]
    [Arguments(1U, 3, 6U, 9)]
    [Arguments(1U, 65520, 0U, 65520)]
    [Arguments(2U, 65528, 0U, 65528)]
    public async Task 非整列位置とメモリ末尾とページ境界へ書き込む_全128bitをリトルエンディアンで指定範囲だけ変更する(
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
        byte[] payload =
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
        payload.CopyTo(expected, effectiveAddress);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            16,
            offset: offset,
            alignment: 4
        );
        var bytes = new byte[expected.Length];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128Store,
            memory,
            in instruction,
            [
                WasmValue.FromI32(address),
                WasmValue.FromV128(0xFEDCBA9876543210UL, 0x0123456789ABCDEFUL),
            ]
        );
        memory.Read(0, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(bytes.SequenceEqual(expected)).IsTrue();
            await Assert.That(memory.PageCount).IsEqualTo(pages);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
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
    public async Task 書き込み範囲がメモリ外または33bit加算で範囲外になる_全メモリを変更せず元の位置を持つtrapを返す(
        uint pages,
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        var before = new byte[pages * 65536];
        Array.Fill(before, (byte)0xA5);
        memory.Write(0, before);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.V128,
            16,
            offset: offset
        );
        var after = new byte[before.Length];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.V128Store,
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
            await Assert.That(memory.PageCount).IsEqualTo(pages);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }
}
