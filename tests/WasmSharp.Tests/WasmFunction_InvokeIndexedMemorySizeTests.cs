using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 混在するメモリのうち32ビットメモリを拡張する_対象ごとのアドレス型でページ数を返す()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateMixedMemoryGrowthInstance(context);

        // Act
        var previous = instance.GetFunction("grow32").Invoke(context, [WasmValue.FromI32(2)]).Values[0];
        var sizes = instance.GetFunction("sizes").Invoke(context, []).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(previous.AsI32()).IsEqualTo(1);
            await Assert.That(sizes[0].AsI32()).IsEqualTo(3);
            await Assert.That(sizes[1].AsI64()).IsEqualTo(2L);
        }
    }

    [Test]
    [Arguments(1L, 2L, 3u)]
    [Arguments(2L, -1L, 2u)]
    [Arguments(1L << 32, -1L, 2u)]
    [Arguments(-1L, -1L, 2u)]
    public async Task 混在するメモリのうち64ビットメモリを拡張する_成否をi64で返し既存ページの内容を保つ(
        long delta,
        long expectedResult,
        uint expectedPages
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var instance = CreateMixedMemoryGrowthInstance(context);
        var memory32 = instance.GetMemory("memory32");
        var memory64 = instance.GetMemory("memory64");
        memory32.Write(65535, [0xA5]);
        memory64.Write(131071, [0x5A]);
        var otherByte = new byte[1];
        var boundary = new byte[1 + (expectedPages - 2) * 65536];

        // Act
        var grow = instance.GetFunction("grow64");
        var result = grow.Invoke(context, [WasmValue.FromI64(delta)]).Values[0];
        var unchanged = grow.Invoke(context, [WasmValue.FromI64(0)]).Values[0];
        var sizes = instance.GetFunction("sizes").Invoke(context, []).Values;
        memory32.Read(65535, otherByte);
        memory64.Read(131071, boundary);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.AsI64()).IsEqualTo(expectedResult);
            await Assert.That(unchanged.AsI64()).IsEqualTo((long)expectedPages);
            await Assert.That(sizes[0].AsI32()).IsEqualTo(1);
            await Assert.That(sizes[1].AsI64()).IsEqualTo((long)expectedPages);
            await Assert.That(otherByte[0]).IsEqualTo((byte)0xA5);
            await Assert.That(boundary[0]).IsEqualTo((byte)0x5A);
            await Assert.That(boundary.AsSpan(1).ContainsAnyExcept((byte)0)).IsFalse();
        }
    }

    [Test]
    [Arguments((byte)0, 1, 2u, 3u)]
    [Arguments((byte)1, 3, 1u, 4u)]
    public async Task Core3でimportまたは定義したメモリを指定する_指定先だけが上限まで拡張される(
        byte memoryIndex,
        int initialSize,
        uint expectedImportedSize,
        uint expectedDefinedSize
    )
    {
        // Arrange
        var context = new WasmExecutionContext();
        var imported = new WasmMemory(new WasmLimits(1, 2));
        var host = new WasmHostModule("env");
        host.Define("imported", imported);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Imports(("env", "imported", 2, HostLinkingModuleBinary.Limits(1, 2))),
            HostLinkingModuleBinary.Functions(0, 0),
            HostLinkingModuleBinary.Memories((3, 4)),
            HostLinkingModuleBinary.Exports(("size", 0, 0), ("grow", 0, 1), ("defined", 2, 1)),
            HostLinkingModuleBinary.Code(([], [0x3F, memoryIndex, 0x0B]), ([], [0x41, 1, 0x40, memoryIndex, 0x0B]))
        );
        var instance = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, [host]);
        var size = instance.GetFunction("size");
        var grow = instance.GetFunction("grow");

        // Act
        var before = size.Invoke(context, []).Values[0].AsI32();
        var previous = grow.Invoke(context, []).Values[0].AsI32();
        var after = size.Invoke(context, []).Values[0].AsI32();
        var exceeded = grow.Invoke(context, []).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before).IsEqualTo(initialSize);
            await Assert.That(previous).IsEqualTo(initialSize);
            await Assert.That(after).IsEqualTo(initialSize + 1);
            await Assert.That(exceeded).IsEqualTo(-1);
            await Assert.That(imported.PageCount).IsEqualTo(expectedImportedSize);
            await Assert.That(instance.GetMemory("defined").PageCount).IsEqualTo(expectedDefinedSize);
        }
    }

    [Test]
    public async Task Core3でメモリ番号0を非最短LEB128で指定する_メモリ0が1ページ拡張される()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var bytes = MemoryGrowModuleBinary.CreateWithNonMinimalZeroIndex();
        var instance = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var grow = instance.GetFunction("grow");

        // Act
        var previous = grow.Invoke(context, []).Values[0].AsI32();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(previous).IsEqualTo(1);
            await Assert.That(instance.GetMemory("memory").PageCount).IsEqualTo(2u);
        }
    }

    private static WasmInstance CreateMixedMemoryGrowthInstance(WasmExecutionContext context)
    {
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F, 0x7E]), ([0x7F], [0x7F]), ([0x7E], [0x7E])),
            HostLinkingModuleBinary.Functions(0, 1, 2),
            HostLinkingModuleBinary.Section(5, [2, 0x01, 1, 3, 0x05, 2, 3]),
            HostLinkingModuleBinary.Exports(
                ("sizes", 0, 0),
                ("grow32", 0, 1),
                ("grow64", 0, 2),
                ("memory32", 2, 0),
                ("memory64", 2, 1)
            ),
            HostLinkingModuleBinary.Code(
                ([], [0x3F, 0, 0x3F, 1, 0x0B]),
                ([], [0x20, 0, 0x40, 0, 0x0B]),
                ([], [0x20, 0, 0x40, 1, 0x0B])
            )
        );
        return WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
    }
}
