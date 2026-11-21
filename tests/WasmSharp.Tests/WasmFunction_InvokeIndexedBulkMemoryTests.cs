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
    public async Task Core3の一括コピーで同じメモリを別名でimportする_重なる範囲のコピー前のバイト列を転送する(
        int destination,
        int source,
        string expected
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var shared = new WasmMemory(new WasmLimits(2));
        var copy = InstantiateBulkMemoryFunction(context, shared, shared, [0xFC, 10, 1, 0]);
        shared.Write(65532, [1, 2, 3, 4, 5, 6, 7, 8]);

        // Act
        copy.Invoke(context, [WasmValue.FromI32(destination), WasmValue.FromI32(source), WasmValue.FromI32(6)]);

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
        var destinationValue = memory64Destination ? WasmValue.FromI64(destination) : WasmValue.FromI32((int)destination);
        var firstBefore = Enumerable.Repeat((byte)0xA0, (int)first.ByteLength).ToArray();
        var secondBefore = Enumerable.Repeat((byte)0xB0, (int)second.ByteLength).ToArray();
        first.Write(0, firstBefore);
        second.Write(0, secondBefore);
        var firstAfter = new byte[firstBefore.Length];
        var secondAfter = new byte[secondBefore.Length];

        // Act & Assert
        var exception = await Assert
            .That(() =>
                function.Invoke(context, [destinationValue, WasmValue.FromI32(sourceOrValue), WasmValue.FromI32(length)])
            )
            .ThrowsExactly<WasmTrapException>();
        first.Read(0, firstAfter);
        second.Read(0, secondAfter);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(firstAfter.AsSpan().CommonPrefixLength(firstBefore)).IsEqualTo(firstBefore.Length);
            await Assert.That(secondAfter.AsSpan().CommonPrefixLength(secondBefore)).IsEqualTo(secondBefore.Length);
        }
    }

    [Test]
    public async Task Core3の一括コピーで容量の異なるメモリの末尾を使う_最終バイトまでの転送と末尾からの長さ0が成功する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var source = new WasmMemory(new WasmLimits(2));
        var destination = new WasmMemory(new WasmLimits(1));
        var copy = InstantiateBulkMemoryFunction(context, source, destination, [0xFC, 10, 1, 0]);
        source.Write(131070, [0x12, 0x34]);

        // Act
        copy.Invoke(context, [WasmValue.FromI32(65534), WasmValue.FromI32(131070), WasmValue.FromI32(2)]);
        copy.Invoke(context, [WasmValue.FromI32(65536), WasmValue.FromI32(131072), WasmValue.FromI32(0)]);

        // Assert
        await Assert.That(ReadMemoryHex(destination, 65533, 3)).IsEqualTo("001234");
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
