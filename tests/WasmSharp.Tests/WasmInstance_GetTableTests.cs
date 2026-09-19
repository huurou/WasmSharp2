using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetTableTests
{
    [Test]
    [Arguments((byte)0x70)]
    [Arguments((byte)0x6F)]
    public async Task 別名と再exportから取得する_更新と増大を同じtableで共有する(byte kind)
    {
        // Arrange
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Tables((kind, 1, 3)),
                    HostLinkingModuleBinary.Exports(("t", 1, 0), ("", 1, 0))
                )
            )
            .Validate()
            .Instantiate([]);
        var original = source.GetTable("t");
        var host = new WasmHostModule("env");
        host.Define("t", original);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "t", 1, [kind, 1, 1, 3])),
                    HostLinkingModuleBinary.Exports(("t", 1, 0), ("alias", 1, 0))
                )
            )
            .Validate();
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);
        var value =
            kind == 0x70
                ? WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([])))
                : WasmValue.FromExternRef(new object());

        // Act
        var resource = first.GetTable("alias");
        resource.Set(0, value);
        var grown = resource.TryGrow(1, value, out _);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(resource).IsSameReferenceAs(original);
            await Assert.That(first.GetTable("t")).IsSameReferenceAs(original);
            await Assert.That(second.GetTable("t")).IsSameReferenceAs(original);
            await Assert.That(source.GetTable("")).IsSameReferenceAs(original);
            await Assert.That(grown).IsTrue();
            await Assert.That(source.GetTable("t").Count).IsEqualTo(2u);
            await Assert.That(second.GetTable("t").Get(0)).IsEqualTo(value);
            await Assert.That(second.GetTable("t").Get(1)).IsEqualTo(value);
        }
        // Act & Assert
        await Assert.That(() => first.GetTable("T")).ThrowsExactly<ArgumentException>();
        var exception = await Assert
            .That(() => first.GetTable(null!))
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
            .That(() => instance.GetTable(name))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
