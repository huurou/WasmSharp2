using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_LoadTests
{
    [Test]
    public async Task I32を非整列位置から読み出す_オフセット先のリトルエンディアンのビット列を積む()
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        memory.Write(9, [0xEF, 0xCD, 0xAB, 0x89]);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.I32,
            4,
            offset: 6,
            alignment: 2
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Load,
            memory,
            in instruction,
            [WasmValue.FromI32(3)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(actual.Value.AsI32()).IsEqualTo(unchecked((int)0x89ABCDEF));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(65532)]
    [Arguments(131064)]
    public async Task I64をページ境界またはメモリ末尾から読み出す_全8バイトを読み出す(int address)
    {
        // Arrange
        var memory = new WasmMemory(new(2, null));
        memory.Write((ulong)address, [0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE]);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.I64,
            8,
            alignment: 3
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Load,
            memory,
            in instruction,
            [WasmValue.FromI32(address)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(actual.Value.AsI64()).IsEqualTo(unchecked((long)0xFEDCBA9876543210));
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(WasmValueKind.I32, (byte)1, true, -127L)]
    [Arguments(WasmValueKind.I32, (byte)1, false, 0x81L)]
    [Arguments(WasmValueKind.I32, (byte)2, true, -32127L)]
    [Arguments(WasmValueKind.I32, (byte)2, false, 0x8281L)]
    [Arguments(WasmValueKind.I64, (byte)1, true, -127L)]
    [Arguments(WasmValueKind.I64, (byte)1, false, 0x81L)]
    [Arguments(WasmValueKind.I64, (byte)2, true, -32127L)]
    [Arguments(WasmValueKind.I64, (byte)2, false, 0x8281L)]
    [Arguments(WasmValueKind.I64, (byte)4, true, -2071756159L)]
    [Arguments(WasmValueKind.I64, (byte)4, false, 0x84838281L)]
    public async Task メモリ末尾の狭い整数の最上位ビットが1_指定した幅だけ読み出して符号拡張またはゼロ拡張する(
        WasmValueKind valueType,
        byte byteWidth,
        bool signExtend,
        long expected
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var address = 65536 - byteWidth;
        memory.Write((ulong)address, new byte[] { 0x81, 0x82, 0x83, 0x84 }.AsSpan(0, byteWidth));
        var instruction = MemoryHandlerFixture.CreateInstruction(valueType, byteWidth, signExtend);

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Load,
            memory,
            in instruction,
            [WasmValue.FromI32(address)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(valueType);
            await Assert
                .That(valueType == WasmValueKind.I32 ? actual.Value.AsI32() : actual.Value.AsI64())
                .IsEqualTo(expected);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(WasmValueKind.I32, (byte)1, 0x7FL)]
    [Arguments(WasmValueKind.I32, (byte)2, 0x7E7FL)]
    [Arguments(WasmValueKind.I64, (byte)1, 0x7FL)]
    [Arguments(WasmValueKind.I64, (byte)2, 0x7E7FL)]
    [Arguments(WasmValueKind.I64, (byte)4, 0x7C7D7E7FL)]
    public async Task 符号付きの狭い整数の最上位ビットが0_正の整数として拡張する(
        WasmValueKind valueType,
        byte byteWidth,
        long expected
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        memory.Write(0, [0x7F, 0x7E, 0x7D, 0x7C]);
        var instruction = MemoryHandlerFixture.CreateInstruction(
            valueType,
            byteWidth,
            signExtend: true
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Load,
            memory,
            in instruction,
            [WasmValue.FromI32(0)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(valueType);
            await Assert
                .That(valueType == WasmValueKind.I32 ? actual.Value.AsI32() : actual.Value.AsI64())
                .IsEqualTo(expected);
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
    public async Task 浮動小数点の通常値と正負の0とNaN_リトルエンディアンの全ビットを保持する(
        WasmValueKind valueType,
        ulong expectedBits,
        byte[] bytes
    )
    {
        // Arrange
        var memory = new WasmMemory(new(2, null));
        memory.Write(65535, bytes);
        var instruction = MemoryHandlerFixture.CreateInstruction(valueType, (byte)bytes.Length);

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Load,
            memory,
            in instruction,
            [WasmValue.FromI32(65535)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value.Kind).IsEqualTo(valueType);
            await Assert
                .That(
                    valueType == WasmValueKind.F32
                        ? actual.Value.AsF32Bits()
                        : actual.Value.AsF64Bits()
                )
                .IsEqualTo(expectedBits);
            await Assert.That(actual.Sentinel).IsEqualTo(NumericHandlerFixture.Sentinel);
            await Assert.That(actual.ValueCount).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(0U, 0, 0U)]
    [Arguments(1U, 65533, 0U)]
    [Arguments(1U, 65536, 0U)]
    [Arguments(1U, -1, 0U)]
    [Arguments(1U, -1, 1U)]
    [Arguments(1U, 1, uint.MaxValue)]
    [Arguments(1U, int.MinValue, 0x80000000U)]
    [Arguments(1U, -1, uint.MaxValue)]
    public async Task 読み出し範囲がメモリ外または33ビット加算で範囲外になる_折り返さず元の関数と命令位置を持つtrapを返す(
        uint pages,
        int address,
        uint offset
    )
    {
        // Arrange
        var memory = new WasmMemory(new(pages, null));
        var instruction = MemoryHandlerFixture.CreateInstruction(
            WasmValueKind.I32,
            4,
            offset: offset
        );

        // Act
        var actual = MemoryHandlerFixture.Execute(
            Interpreter.Load,
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
