using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("0204", 0x44332211, "78563412", "55667708", false)]
    [Arguments("420004", 0x44332211, "78563412", "55667708", false)]
    [Arguments("C200810084808080808080808000", 0x08776655, "11223344", "78563412", false)]
    [Arguments("420104", 0x08776655, "11223344", "78563412", true)]
    public async Task Core3複数メモリで通常のロードとストアを呼ぶ_指定したメモリの同じアドレスを読み書きする(
        string memarg,
        int expectedLoad,
        string expectedFirst,
        string expectedSecond,
        bool memory64
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var argument = Convert.FromHexString(memarg);
        var instance = CreateMemoryAccessInstance(
            context,
            0x7F,
            [0x28, .. argument],
            [0x36, .. argument],
            memory64: memory64
        );
        var address = memory64 ? WasmValue.FromI64(7) : WasmValue.FromI32(7);
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(11, Convert.FromHexString("11223344"));
        second.Write(11, Convert.FromHexString("55667708"));

        // Act
        var loaded = instance.GetFunction("load").Invoke(context, [address]).Values[0].AsI32();
        instance.GetFunction("store").Invoke(context, [address, WasmValue.FromI32(0x12345678)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded).IsEqualTo(expectedLoad);
            await Assert.That(ReadMemoryHex(first, 11, 4)).IsEqualTo(expectedFirst);
            await Assert.That(ReadMemoryHex(second, 11, 4)).IsEqualTo(expectedSecond);
        }
    }

    [Test]
    [Arguments(
        "0404",
        "000102030405060708090A0B0C0D0E0F",
        "FFEEDDCCBBAA99887766554433221100",
        "101112131415161718191A1B1C1D1E1F",
        false
    )]
    [Arguments(
        "440104",
        "101112131415161718191A1B1C1D1E1F",
        "000102030405060708090A0B0C0D0E0F",
        "FFEEDDCCBBAA99887766554433221100",
        false
    )]
    [Arguments(
        "440104",
        "101112131415161718191A1B1C1D1E1F",
        "000102030405060708090A0B0C0D0E0F",
        "FFEEDDCCBBAA99887766554433221100",
        true
    )]
    public async Task Core3複数メモリでv128をロードとストアする_指定したメモリの16バイトを読み書きする(
        string memarg,
        string expectedLoad,
        string expectedFirst,
        string expectedSecond,
        bool memory64
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var argument = Convert.FromHexString(memarg);
        var instance = CreateMemoryAccessInstance(
            context,
            0x7B,
            [0xFD, 0, .. argument],
            [0xFD, 0x0B, .. argument],
            memory64: memory64
        );
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(11, Convert.FromHexString("000102030405060708090A0B0C0D0E0F"));
        second.Write(11, Convert.FromHexString("101112131415161718191A1B1C1D1E1F"));
        var address = memory64 ? WasmValue.FromI64(7) : WasmValue.FromI32(7);

        // Act
        var loaded = instance.GetFunction("load").Invoke(context, [address]).Values[0];
        instance.GetFunction("store").Invoke(context, [address, Vector("FFEEDDCCBBAA99887766554433221100")]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded).IsEqualTo(Vector(expectedLoad));
            await Assert.That(ReadMemoryHex(first, 11, 16)).IsEqualTo(expectedFirst);
            await Assert.That(ReadMemoryHex(second, 11, 16)).IsEqualTo(expectedSecond);
        }
    }

    [Test]
    [Arguments((byte)0x09, "81028304810283048102830481028304")] // v128.load32_splat
    [Arguments((byte)0x5C, "81028304000000000000000000000000")] // v128.load32_zero
    [Arguments((byte)0x02, "81000200830004008500060087000800")] // v128.load8x8_u
    public async Task Core3でSIMDロードにメモリ1を指定する_複製とゼロ埋めと符号なし拡張の各命令が指定先の値を使う(
        byte loadOpcode,
        string expected
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateMemoryAccessInstance(context, 0x7B, [0xFD, loadOpcode, 0x40, 1, 4], [0xFD, 0x0B, 4, 0]);
        instance.GetMemory("first").Write(11, [1, 2, 3, 4, 5, 6, 7, 8]);
        instance.GetMemory("second").Write(11, [0x81, 2, 0x83, 4, 0x85, 6, 0x87, 8]);

        // Act
        var loaded = instance.GetFunction("load").Invoke(context, [WasmValue.FromI32(7)]).Values[0];

        // Assert
        await Assert.That(loaded).IsEqualTo(Vector(expected));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Core3複数メモリでv128のlaneを転送する_指定したメモリのオフセットとlane番号を区別する(bool memory64)
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateMemoryAccessInstance(
            context,
            0x7B,
            [0xFD, 0x55, 0x41, 1, 5, 3],
            [0xFD, 0x59, 0x41, 1, 5, 3],
            lane: true,
            memory64: memory64
        );
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(11, Convert.FromHexString("A1A2A3A4"));
        second.Write(11, Convert.FromHexString("B1B2B3B4"));
        var address = memory64 ? WasmValue.FromI64(7) : WasmValue.FromI32(7);
        var vector = Vector("101112131415161718191A1B1C1D1E1F");

        // Act
        var loaded = instance.GetFunction("load").Invoke(context, [address, vector]).Values[0];
        instance.GetFunction("store").Invoke(context, [address, vector]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded).IsEqualTo(Vector("101112131415B2B318191A1B1C1D1E1F"));
            await Assert.That(ReadMemoryHex(first, 11, 4)).IsEqualTo("A1A2A3A4");
            await Assert.That(ReadMemoryHex(second, 11, 4)).IsEqualTo("B11617B4");
        }
    }

    [Test]
    [Arguments("load", false, "400104", 65530L, false)]
    [Arguments("store", false, "400104", 65530L, false)]
    [Arguments("load", true, "400104", 65530L, false)]
    [Arguments("store", true, "400104", 65530L, false)]
    [Arguments("load", false, "4001FFFFFFFF0F", 1L, false)]
    [Arguments("store", false, "4001FFFFFFFF0F", 1L, false)]
    [Arguments("load", false, "40018080808010", 0L, true)]
    [Arguments("store", true, "40018080808010", 0L, true)]
    [Arguments("store", false, "400100", 0x1_0000_0000L, true)]
    [Arguments("load", true, "400101", -1L, true)]
    [Arguments("store", false, "4001FFFFFFFFFFFFFFFFFF01", 1L, true)]
    [Arguments("store", true, "400104", 65530L, true)]
    public async Task Core3複数メモリのロードまたはストアが選択先の範囲を超える_両方のメモリの内容を保ってtrapする(
        string operation,
        bool simd,
        string memarg,
        long address,
        bool memory64
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var argument = Convert.FromHexString(memarg);
        var instance = simd
            ? CreateMemoryAccessInstance(
                context,
                0x7B,
                [0xFD, 0, .. argument],
                [0xFD, 0x0B, .. argument],
                memory64: memory64
            )
            : CreateMemoryAccessInstance(context, 0x7F, [0x28, .. argument], [0x36, .. argument], memory64: memory64);
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(65534, Convert.FromHexString("00112233445566778899AABBCCDDEEFF"));
        second.Write(65534, Convert.FromHexString("A1B2"));
        first.Write(0, Convert.FromHexString("10203040"));
        second.Write(0, Convert.FromHexString("A0B0C0D0"));
        var operand = memory64 ? WasmValue.FromI64(address) : WasmValue.FromI32((int)address);
        var value = simd ? Vector("FFEEDDCCBBAA99887766554433221100") : WasmValue.FromI32(0x12345678);
        var function = instance.GetFunction(operation);
        WasmValue[] arguments = operation == "load" ? [operand] : [operand, value];

        // Act & Assert
        var trap = await Assert.That(() => function.Invoke(context, arguments)).ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(trap!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(ReadMemoryHex(first, 65534, 16)).IsEqualTo("00112233445566778899AABBCCDDEEFF");
            await Assert.That(ReadMemoryHex(second, 65534, 2)).IsEqualTo("A1B2");
            await Assert.That(ReadMemoryHex(first, 0, 4)).IsEqualTo("10203040");
            await Assert.That(ReadMemoryHex(second, 0, 4)).IsEqualTo("A0B0C0D0");
        }
    }

    private static WasmInstance CreateMemoryAccessInstance(
        WasmExecutionContext context,
        byte valueType,
        byte[] load,
        byte[] store,
        bool lane = false,
        bool memory64 = false
    )
    {
        var addressType = memory64 ? (byte)0x7E : (byte)0x7F;
        var secondMemoryFlags = memory64 ? (byte)0x04 : (byte)0x00;
        byte[] loadParameters = lane ? [addressType, valueType] : [addressType];
        byte[] loadOperands = lane ? [0x20, 0, 0x20, 1] : [0x20, 0];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((loadParameters, [valueType]), ([addressType, valueType], [])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Section(5, [2, 0, 2, secondMemoryFlags, 1]),
            HostLinkingModuleBinary.Exports(("load", 0, 0), ("store", 0, 1), ("first", 2, 0), ("second", 2, 1)),
            HostLinkingModuleBinary.Code(([], [.. loadOperands, .. load, 0x0B]), ([], [0x20, 0, 0x20, 1, .. store, 0x0B]))
        );
        return WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
    }

    private static string ReadMemoryHex(WasmMemory memory, ulong offset, int length)
    {
        var bytes = new byte[length];
        memory.Read(offset, bytes);
        return Convert.ToHexString(bytes);
    }
}
