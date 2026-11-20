using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
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
    public async Task メモリ番号0を非最短LEB128で指定する_Core3では拡張できCore2ではデコードで拒否する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, 2)),
            HostLinkingModuleBinary.Exports(("grow", 0, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Code(([], [0x41, 1, 0x40, 0x80, 0x00, 0x0B]))
        );

        // Act
        var instance = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var previous = instance.GetFunction("grow").Invoke(context, []).Values[0].AsI32();

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(previous).IsEqualTo(1);
            await Assert.That(instance.GetMemory("memory").PageCount).IsEqualTo(2u);
            await Assert.That(exception!.Message).StartsWith("zero byte expected");
        }
    }
}
