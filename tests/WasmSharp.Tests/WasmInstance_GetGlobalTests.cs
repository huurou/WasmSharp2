using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetGlobalTests
{
    [Test]
    public async Task 共有globalを更新する_反復取得で現在値を返す()
    {
        // Arrange
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 1, 0x0B])),
                    HostLinkingModuleBinary.Exports(("g", 3, 0))
                )
            )
            .Validate()
            .Instantiate([]);
        var before = instance.GetGlobal("g");

        // Act
        instance.GetGlobalResource("g").Value = WasmValue.FromI32(2);
        var after = instance.GetGlobal("g");

        // Assert
        await Assert.That(before.AsI32()).IsEqualTo(1);
        await Assert.That(after.AsI32()).IsEqualTo(2);
        // Act & Assert
        await Assert.That(() => instance.GetGlobal("G")).ThrowsExactly<ArgumentException>();
        var exception = await Assert
            .That(() => instance.GetGlobal(null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    [Arguments("missing")]
    [Arguments("run")]
    [Arguments("")]
    public async Task 指定した種類のexportがない_名前の引数不正として拒否する(string name)
    {
        // Arrange
        var instance = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetGlobal(name))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
