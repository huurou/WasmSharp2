using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("0204", 0x44332211, "78563412", "55667708")]
    [Arguments("420004", 0x44332211, "78563412", "55667708")]
    [Arguments("C200810084808080808080808000", 0x08776655, "11223344", "78563412")]
    public async Task Core3複数メモリで通常のロードとストアを呼ぶ_指定したメモリの同じアドレスを読み書きする(
        string memarg,
        int expectedLoad,
        string expectedFirst,
        string expectedSecond
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var argument = Convert.FromHexString(memarg);
        var instance = CreateMemoryAccessInstance(context, 0x7F, [0x28, .. argument], [0x36, .. argument]);
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(11, Convert.FromHexString("11223344"));
        second.Write(11, Convert.FromHexString("55667708"));

        // Act
        var loaded = instance.GetFunction("load").Invoke(context, [WasmValue.FromI32(7)]).Values[0].AsI32();
        instance.GetFunction("store").Invoke(context, [WasmValue.FromI32(7), WasmValue.FromI32(0x12345678)]);

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
        "101112131415161718191A1B1C1D1E1F"
    )]
    [Arguments(
        "440104",
        "101112131415161718191A1B1C1D1E1F",
        "000102030405060708090A0B0C0D0E0F",
        "FFEEDDCCBBAA99887766554433221100"
    )]
    public async Task Core3複数メモリでv128をロードとストアする_指定したメモリの16バイトを読み書きする(
        string memarg,
        string expectedLoad,
        string expectedFirst,
        string expectedSecond
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var argument = Convert.FromHexString(memarg);
        var instance = CreateMemoryAccessInstance(context, 0x7B, [0xFD, 0, .. argument], [0xFD, 0x0B, .. argument]);
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(11, Convert.FromHexString("000102030405060708090A0B0C0D0E0F"));
        second.Write(11, Convert.FromHexString("101112131415161718191A1B1C1D1E1F"));
        var address = WasmValue.FromI32(7);

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
    public async Task Core3複数メモリでv128のlaneを転送する_指定したメモリのオフセットとlane番号を区別する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateMemoryAccessInstance(
            context,
            0x7B,
            [0xFD, 0x55, 0x41, 1, 5, 3],
            [0xFD, 0x59, 0x41, 1, 5, 3],
            lane: true
        );
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(11, Convert.FromHexString("A1A2A3A4"));
        second.Write(11, Convert.FromHexString("B1B2B3B4"));
        var address = WasmValue.FromI32(7);
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
    [Arguments(false, "400104", 65530)]
    [Arguments(true, "400104", 65530)]
    [Arguments(false, "4001FFFFFFFF0F", 1)]
    public async Task Core3複数メモリで選択先の範囲を超えて読み書きする_両方のメモリの内容を保ってtrapする(
        bool simd,
        string memarg,
        int address
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var argument = Convert.FromHexString(memarg);
        var instance = simd
            ? CreateMemoryAccessInstance(context, 0x7B, [0xFD, 0, .. argument], [0xFD, 0x0B, .. argument])
            : CreateMemoryAccessInstance(context, 0x7F, [0x28, .. argument], [0x36, .. argument]);
        var first = instance.GetMemory("first");
        var second = instance.GetMemory("second");
        first.Write(65534, Convert.FromHexString("00112233445566778899AABBCCDDEEFF"));
        second.Write(65534, Convert.FromHexString("A1B2"));
        var operand = WasmValue.FromI32(address);
        var value = simd ? Vector("FFEEDDCCBBAA99887766554433221100") : WasmValue.FromI32(0x12345678);

        // Act & Assert
        var loadTrap = await Assert
            .That(() => instance.GetFunction("load").Invoke(context, [operand]))
            .ThrowsExactly<WasmTrapException>();
        var storeTrap = await Assert
            .That(() => instance.GetFunction("store").Invoke(context, [operand, value]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(loadTrap!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(storeTrap!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(ReadMemoryHex(first, 65534, 16)).IsEqualTo("00112233445566778899AABBCCDDEEFF");
            await Assert.That(ReadMemoryHex(second, 65534, 2)).IsEqualTo("A1B2");
        }
    }

    private static WasmInstance CreateMemoryAccessInstance(
        WasmExecutionContext context,
        byte valueType,
        byte[] load,
        byte[] store,
        bool lane = false
    )
    {
        byte[] loadParameters = lane ? [0x7F, valueType] : [0x7F];
        byte[] loadOperands = lane ? [0x20, 0, 0x20, 1] : [0x20, 0];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((loadParameters, [valueType]), ([0x7F, valueType], [])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Memories((2, null), (1, null)),
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
