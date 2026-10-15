using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_StoreTests
{
    [Test]
    public async Task I64を非整列位置へ書き込む_オフセット先のリトルエンディアンのビット列を変更する()
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.I64,
            8,
            offset: 6,
            alignment: 3
        );
        var bytes = new byte[10];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Store,
            memory,
            in instruction,
            [WasmValue.FromI32(3), WasmValue.FromI64(unchecked((long)0xFEDCBA9876543210))]
        );
        memory.Read(8, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert
                .That(
                    bytes.SequenceEqual(
                        new byte[] { 0, 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE, 0 }
                    )
                )
                .IsTrue();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(65534)]
    [Arguments(131068)]
    public async Task I32をページ境界またはメモリ末尾へ書き込む_全4バイトを変更する(int address)
    {
        // Arrange
        var memory = new WasmMemory(new(2, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.I32,
            4,
            alignment: 2
        );
        var bytes = new byte[4];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Store,
            memory,
            in instruction,
            [WasmValue.FromI32(address), WasmValue.FromI32(unchecked((int)0x89ABCDEF))]
        );
        memory.Read((ulong)address, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(bytes.SequenceEqual(new byte[] { 0xEF, 0xCD, 0xAB, 0x89 })).IsTrue();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(WasmValueKind.I32, (byte)1, new byte[] { 0x98 })]
    [Arguments(WasmValueKind.I32, (byte)2, new byte[] { 0x98, 0xBA })]
    [Arguments(WasmValueKind.I64, (byte)1, new byte[] { 0x10 })]
    [Arguments(WasmValueKind.I64, (byte)2, new byte[] { 0x10, 0x32 })]
    [Arguments(WasmValueKind.I64, (byte)4, new byte[] { 0x10, 0x32, 0x54, 0x76 })]
    public async Task 値型より狭い幅へ書き込む_指定した下位ビットだけを書き込み前後のバイトを保持する(
        WasmValueKind valueType,
        byte byteWidth,
        byte[] expectedBytes
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        memory.Write(7, [0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5]);
        var instruction = MemoryHandlerFixture.CreateInstruction(valueType, byteWidth);
        var value =
            valueType == WasmValueKind.I32
                ? WasmValue.FromI32(unchecked((int)0xFEDCBA98))
                : WasmValue.FromI64(unchecked((long)0xFEDCBA9876543210));
        var expected = new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 };
        expectedBytes.CopyTo(expected, 2);
        var bytes = new byte[8];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Store,
            memory,
            in instruction,
            [WasmValue.FromI32(9), value]
        );
        memory.Read(7, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(bytes.SequenceEqual(expected)).IsTrue();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(WasmValueKind.F32, 0x3F800001UL, new byte[] { 0x01, 0, 0x80, 0x3F })]
    [Arguments(WasmValueKind.F32, 0x00000000UL, new byte[] { 0, 0, 0, 0 })]
    [Arguments(WasmValueKind.F32, 0x80000000UL, new byte[] { 0, 0, 0, 0x80 })]
    [Arguments(WasmValueKind.F32, 0x7F812345UL, new byte[] { 0x45, 0x23, 0x81, 0x7F })]
    [Arguments(WasmValueKind.F32, 0xFF812345UL, new byte[] { 0x45, 0x23, 0x81, 0xFF })]
    [Arguments(WasmValueKind.F32, 0x7FC54321UL, new byte[] { 0x21, 0x43, 0xC5, 0x7F })]
    [Arguments(WasmValueKind.F32, 0xFFC54321UL, new byte[] { 0x21, 0x43, 0xC5, 0xFF })]
    [Arguments(
        WasmValueKind.F64,
        0x3FF0000000000001UL,
        new byte[] { 1, 0, 0, 0, 0, 0, 0xF0, 0x3F }
    )]
    [Arguments(WasmValueKind.F64, 0x0000000000000000UL, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
    [Arguments(WasmValueKind.F64, 0x8000000000000000UL, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80 })]
    [Arguments(
        WasmValueKind.F64,
        0x7FF0123456789ABCUL,
        new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0xF0, 0x7F }
    )]
    [Arguments(
        WasmValueKind.F64,
        0xFFF0123456789ABCUL,
        new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0xF0, 0xFF }
    )]
    [Arguments(
        WasmValueKind.F64,
        0x7FF8543210ABCDEFUL,
        new byte[] { 0xEF, 0xCD, 0xAB, 0x10, 0x32, 0x54, 0xF8, 0x7F }
    )]
    [Arguments(
        WasmValueKind.F64,
        0xFFF8543210ABCDEFUL,
        new byte[] { 0xEF, 0xCD, 0xAB, 0x10, 0x32, 0x54, 0xF8, 0xFF }
    )]
    public async Task 浮動小数点の通常値と正負の0とNaN_全ビットをリトルエンディアンで書き込む(
        WasmValueKind valueType,
        ulong bits,
        byte[] expectedBytes
    )
    {
        // Arrange
        var memory = new WasmMemory(new(2, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            valueType,
            (byte)expectedBytes.Length
        );
        var value =
            valueType == WasmValueKind.F32
                ? WasmValue.FromF32Bits((uint)bits)
                : WasmValue.FromF64Bits(bits);
        var bytes = new byte[expectedBytes.Length];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Store,
            memory,
            in instruction,
            [WasmValue.FromI32(65535), value]
        );
        memory.Read(65535, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(bytes.SequenceEqual(expectedBytes)).IsTrue();
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(65533, 0U)]
    [Arguments(65536, 0U)]
    [Arguments(-1, 0U)]
    [Arguments(-1, 1U)]
    [Arguments(1, uint.MaxValue)]
    [Arguments(int.MinValue, 0x80000000U)]
    [Arguments(-1, uint.MaxValue)]
    public async Task 書き込み範囲がメモリ外または33ビット加算で範囲外になる_全メモリを変更せず元の位置を持つtrapを返す(
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var before = new byte[65536];
        Array.Fill(before, (byte)0xA5);
        memory.Write(0, before);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.I32,
            4,
            offset: offset
        );
        var after = new byte[before.Length];

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Store,
            memory,
            in instruction,
            [WasmValue.FromI32(address), WasmValue.FromI32(0x12345678)]
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
