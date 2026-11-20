using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
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
