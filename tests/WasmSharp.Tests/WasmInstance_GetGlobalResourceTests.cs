using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetGlobalResourceTests
{
    [Test]
    public async Task 別名と再exportから取得する_定義の独立性とimportの共有を保つ()
    {
        // Arrange
        var shared = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7));
        var host = new WasmHostModule("env");
        host.Define("g", shared);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 1])),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 42, 0x0B])),
                    HostLinkingModuleBinary.Exports(("shared", 3, 0), ("g", 3, 1), ("", 3, 1))
                )
            )
            .Validate();
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);

        // Act
        var resource = first.GetGlobalResource("g");
        var relayHost = new WasmHostModule("relay");
        relayHost.Define("g", resource);
        var relay = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("relay", "g", 3, [0x7F, 1])),
                    HostLinkingModuleBinary.Exports(("g", 3, 0))
                )
            )
            .Validate()
            .Instantiate([relayHost]);
        relay.GetGlobalResource("g").Value = WasmValue.FromI32(99);
        first.GetGlobalResource("shared").Value = WasmValue.FromI32(17);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetGlobalResource("g")).IsSameReferenceAs(resource);
            await Assert.That(first.GetGlobalResource("")).IsSameReferenceAs(resource);
            await Assert.That(relay.GetGlobalResource("g")).IsSameReferenceAs(resource);
            await Assert.That(ReferenceEquals(second.GetGlobalResource("g"), resource)).IsFalse();
            await Assert.That(first.GetGlobal("g").AsI32()).IsEqualTo(99);
            await Assert.That(second.GetGlobal("g").AsI32()).IsEqualTo(42);
            await Assert.That(second.GetGlobalResource("shared")).IsSameReferenceAs(shared);
            await Assert.That(second.GetGlobal("shared").AsI32()).IsEqualTo(17);
        }
    }

    [Test]
    [Arguments("missing")]
    [Arguments("run")]
    [Arguments("")]
    public async Task 名前が存在しないか種類が違う_名前の引数例外で拒否する(string name)
    {
        // Arrange
        var instance = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 1, 0x0B))
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetGlobalResource(name))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    public async Task 名前がnullである_名前のnull引数例外で拒否する()
    {
        // Arrange
        var instance = WasmModule
            .Decode(HostLinkingModuleBinary.Create())
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetGlobalResource(null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
