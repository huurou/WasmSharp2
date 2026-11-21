using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("FC080201", 1, "50203080")]
    [Arguments("FC0B01", 0x123, "50232380")]
    public async Task Core3の一括初期化または充填でメモリ1を指定する_指定範囲だけに書き込む(
        string operation,
        int sourceOrValue,
        string expected
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var first = new WasmMemory(new WasmLimits(1));
        var second = new WasmMemory(new WasmLimits(1));
        var function = InstantiateBulkMemoryFunction(context, first, second, Convert.FromHexString(operation));
        first.Write(0, [0xAA, 0xBB, 0xCC, 0xDD]);
        second.Write(0, [0x50, 0x60, 0x70, 0x80]);

        // Act
        function.Invoke(context, [WasmValue.FromI32(1), WasmValue.FromI32(sourceOrValue), WasmValue.FromI32(2)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadMemoryHex(first, 0, 4)).IsEqualTo("AABBCCDD");
            await Assert.That(ReadMemoryHex(second, 0, 4)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task Memory64の一括初期化で部分範囲と末尾の長さ0を指定する_対象メモリの最終バイトまで転送する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var first = new WasmMemory(new WasmLimits(1));
        var second = WasmMemory.Create(new WasmMemoryType(WasmValueKind.I64, 1));
        var init = InstantiateBulkMemoryFunction(context, first, second, [0xFC, 8, 2, 1], [0x7E, 0x7F, 0x7F]);
        first.Write(65533, [0xA1, 0xA2, 0xA3]);
        second.Write(65533, [0xB1, 0xB2, 0xB3]);

        // Act
        init.Invoke(context, [WasmValue.FromI64(65534), WasmValue.FromI32(1), WasmValue.FromI32(2)]);
        init.Invoke(context, [WasmValue.FromI64(65536), WasmValue.FromI32(4), WasmValue.FromI32(0)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadMemoryHex(first, 65533, 3)).IsEqualTo("A1A2A3");
            await Assert.That(ReadMemoryHex(second, 65533, 3)).IsEqualTo("B12030");
        }
    }

    [Test]
    public async Task Memory64の一括充填で最終バイトまでと末尾の長さ0を指定する_指定範囲を値の下位8ビットで埋める()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var first = new WasmMemory(new WasmLimits(0));
        var second = WasmMemory.Create(new WasmMemoryType(WasmValueKind.I64, 1));
        var fill = InstantiateBulkMemoryFunction(context, first, second, [0xFC, 11, 1], [0x7E, 0x7F, 0x7E]);
        second.Write(65532, [0x11, 0x22, 0x33, 0x44]);

        // Act
        fill.Invoke(context, [WasmValue.FromI64(65533), WasmValue.FromI32(0x123), WasmValue.FromI64(3)]);
        fill.Invoke(context, [WasmValue.FromI64(65536), WasmValue.FromI32(0), WasmValue.FromI64(0)]);

        // Assert
        await Assert.That(ReadMemoryHex(second, 65532, 4)).IsEqualTo("11232323");
    }

    [Test]
    [Arguments(65534, 65534)]
    [Arguments(65533, 65535)]
    [Arguments(65535, 65533)]
    public async Task Core3の一括コピーで異なるメモリを指定する_コピー元を保ちコピー先の指定範囲に転送する(
        int destination,
        int source
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var first = new WasmMemory(new WasmLimits(2));
        var second = new WasmMemory(new WasmLimits(2));
        var copy = InstantiateBulkMemoryFunction(context, first, second, [0xFC, 10, 1, 0]);
        first.Write((ulong)source - 1, [0xA0, 1, 2, 3, 4, 0xAF]);
        second.Write((ulong)destination - 1, [0xB0, 5, 6, 7, 8, 0xBF]);

        // Act
        copy.Invoke(context, [WasmValue.FromI32(destination), WasmValue.FromI32(source), WasmValue.FromI32(4)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadMemoryHex(first, (ulong)source - 1, 6)).IsEqualTo("A001020304AF");
            await Assert.That(ReadMemoryHex(second, (ulong)destination - 1, 6)).IsEqualTo("B001020304BF");
        }
    }

    [Test]
    [Arguments(65534, 65532, "0102010203040506")]
    [Arguments(65532, 65534, "0304050607080708")]
    [Arguments(65534, 65532, "0102010203040506", WasmValueKind.I64)]
    [Arguments(65532, 65534, "0304050607080708", WasmValueKind.I64)]
    public async Task Core3の一括コピーで同じメモリを別名でimportする_重なる範囲のコピー前のバイト列を転送する(
        int destination,
        int source,
        string expected,
        WasmValueKind addressType = WasmValueKind.I32
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var shared = WasmMemory.Create(new WasmMemoryType(addressType, 2));
        var copy = InstantiateBulkMemoryFunction(
            context,
            shared,
            shared,
            [0xFC, 10, 1, 0],
            addressType == WasmValueKind.I64 ? [0x7E, 0x7E, 0x7E] : null
        );
        shared.Write(65532, [1, 2, 3, 4, 5, 6, 7, 8]);

        // Act
        InvokeBulkMemoryFunction(context, copy, destination, source, 6);

        // Assert
        await Assert.That(ReadMemoryHex(shared, 65532, 8)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("FC080201", 65535, 0)]
    [Arguments("FC080201", 12, 3)]
    [Arguments("FC0B01", 65535, 0xEF)]
    [Arguments("FC0A0100", 65535, 12)]
    [Arguments("FC0A0001", 12, 65535)]
    [Arguments("FC080201", 65535L, 0, true)]
    [Arguments("FC080201", 12L, 3, true)]
    [Arguments("FC080201", 65537L, 4, true, 0)]
    [Arguments("FC080201", 1L << 32, 0, true)]
    public async Task Core3の一括操作の範囲がメモリまたはデータの末尾を超える_trap後も両方のメモリの内容を保つ(
        string operation,
        long destination,
        int sourceOrValue,
        bool memory64Destination = false,
        int length = 2
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var first = new WasmMemory(new WasmLimits(2));
        var second = WasmMemory.Create(new WasmMemoryType(memory64Destination ? WasmValueKind.I64 : WasmValueKind.I32, 1));
        var function = InstantiateBulkMemoryFunction(
            context,
            first,
            second,
            Convert.FromHexString(operation),
            memory64Destination ? [0x7E, 0x7F, 0x7F] : null
        );

        var firstBefore = InitializeMemory(first, 0xA0);
        var secondBefore = InitializeMemory(second, 0xB0);

        // Act & Assert
        var exception = await Assert
            .That(() => InvokeBulkMemoryFunction(context, function, destination, sourceOrValue, length))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await AssertMemoryUnchanged(first, firstBefore);
            await AssertMemoryUnchanged(second, secondBefore);
        }
    }

    [Test]
    [Arguments("FC0B01", "7E7F7E", 1L << 32, 0x123, 1)]
    [Arguments("FC0B01", "7E7F7E", 12, 0x123, (1L << 32) + 1)]
    [Arguments("FC0B01", "7E7F7E", 65535, 0x123, 2)]
    [Arguments("FC0B01", "7E7F7E", 65537, 0x123, 0)]
    [Arguments("FC0A0100", "7E7F7F", 1L << 32, 12, 1)]
    [Arguments("FC0A0100", "7F7E7F", 12, 1L << 32, 1)]
    [Arguments("FC0A0100", "7E7E7E", 1L << 32, 12, 1)]
    [Arguments("FC0A0100", "7E7E7E", 12, 1L << 32, 1)]
    [Arguments("FC0A0100", "7E7E7E", 12, 12, (1L << 32) + 1)]
    [Arguments("FC0A0100", "7E7E7E", 65535, 12, 2)]
    [Arguments("FC0A0100", "7E7E7E", 12, 65535, 2)]
    [Arguments("FC0A0100", "7E7E7E", 65537, 65536, 0)]
    [Arguments("FC0A0100", "7E7E7E", 65536, 65537, 0)]
    public async Task Memory64を使う充填またはコピーの位置や長さが範囲を超える_trap後も両メモリの全バイトを保つ(
        string operation,
        string parameterTypes,
        long destination,
        long sourceOrValue,
        long length
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var parameters = Convert.FromHexString(parameterTypes);
        var sourceType = parameters[1] == 0x7E ? WasmValueKind.I64 : WasmValueKind.I32;
        var destinationType = parameters[0] == 0x7E ? WasmValueKind.I64 : WasmValueKind.I32;
        var first = WasmMemory.Create(new WasmMemoryType(sourceType, 1));
        var second = WasmMemory.Create(new WasmMemoryType(destinationType, 1));
        var function = InstantiateBulkMemoryFunction(context, first, second, Convert.FromHexString(operation), parameters);

        var firstBefore = InitializeMemory(first, 0xA0);
        var secondBefore = InitializeMemory(second, 0xB0);

        // Act & Assert
        var exception = await Assert
            .That(() => InvokeBulkMemoryFunction(context, function, destination, sourceOrValue, length))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await AssertMemoryUnchanged(first, firstBefore);
            await AssertMemoryUnchanged(second, secondBefore);
        }
    }

    [Test]
    [Arguments(WasmValueKind.I32, WasmValueKind.I32, "7F7F7F")]
    [Arguments(WasmValueKind.I64, WasmValueKind.I32, "7E7F7F")]
    [Arguments(WasmValueKind.I32, WasmValueKind.I64, "7F7E7F")]
    [Arguments(WasmValueKind.I64, WasmValueKind.I64, "7E7E7E")]
    public async Task Core3の一括コピーで容量の異なるメモリの末尾を使う_指定範囲だけを転送し末尾の長さ0も受理する(
        WasmValueKind destinationType,
        WasmValueKind sourceType,
        string parameterTypes
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var source = WasmMemory.Create(new WasmMemoryType(sourceType, 2));
        var destination = WasmMemory.Create(new WasmMemoryType(destinationType, 1));
        var copy = InstantiateBulkMemoryFunction(
            context,
            source,
            destination,
            [0xFC, 10, 1, 0],
            Convert.FromHexString(parameterTypes)
        );
        source.Write(131069, [0xA0, 0x12, 0x34]);
        destination.Write(65533, [0xB0, 0x56, 0x78]);

        // Act
        InvokeBulkMemoryFunction(context, copy, 65534, 131070, 2);
        InvokeBulkMemoryFunction(context, copy, 65536, 131072, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadMemoryHex(source, 131069, 3)).IsEqualTo("A01234");
            await Assert.That(ReadMemoryHex(destination, 65533, 3)).IsEqualTo("B01234");
        }
    }

    private static byte[] InitializeMemory(WasmMemory memory, byte value)
    {
        var contents = Enumerable.Repeat(value, (int)memory.ByteLength).ToArray();
        memory.Write(0, contents);
        return contents;
    }

    private static async Task AssertMemoryUnchanged(WasmMemory memory, byte[] before)
    {
        var after = new byte[before.Length];
        memory.Read(0, after);
        await Assert.That(after.AsSpan().CommonPrefixLength(before)).IsEqualTo(before.Length);
    }

    private static void InvokeBulkMemoryFunction(
        WasmExecutionContext context,
        WasmFunction function,
        long destination,
        long sourceOrValue,
        long length
    )
    {
        function.Invoke(context, [Argument(0, destination), Argument(1, sourceOrValue), Argument(2, length)]);

        WasmValue Argument(int index, long value)
        {
            return function.Type.Parameters[index] == WasmValueKind.I64
                ? WasmValue.FromI64(value)
                : WasmValue.FromI32(checked((int)value));
        }
    }

    private static WasmFunction InstantiateBulkMemoryFunction(
        WasmExecutionContext context,
        WasmMemory first,
        WasmMemory second,
        byte[] operation,
        byte[]? parameterTypes = null
    )
    {
        var host = new WasmHostModule("env");
        host.Define("first", first);
        host.Define("second", second);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((parameterTypes ?? [0x7F, 0x7F, 0x7F], [])),
            HostLinkingModuleBinary.Imports(
                ("env", "first", 2, ImportLimits(first)),
                ("env", "second", 2, ImportLimits(second))
            ),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("bulk", 0, 0)),
            HostLinkingModuleBinary.Section(12, [3]),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. operation, 0x0B])),
            HostLinkingModuleBinary.Section(11, [3, 1, 1, 0xA0, 1, 1, 0xB0, 1, 4, 0x10, 0x20, 0x30, 0x40])
        );
        return WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, [host]).GetFunction("bulk");

        static byte[] ImportLimits(WasmMemory memory)
        {
            return memory.Type.AddressType == WasmValueKind.I64 ? [4, 0] : [0, 0];
        }
    }
}
