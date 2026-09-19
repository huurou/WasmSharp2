using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetMemoryTests
{
    [Test]
    public async Task 別名と再exportから取得する_更新と増大を同じmemoryで共有する()
    {
        // Arrange
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Memories((1, 3)),
                    HostLinkingModuleBinary.Exports(("m", 2, 0), ("", 2, 0))
                )
            )
            .Validate()
            .Instantiate([]);
        var original = source.GetMemory("m");
        var host = new WasmHostModule("env");
        host.Define("m", original);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "m", 2, [1, 1, 3])),
                    HostLinkingModuleBinary.Exports(("m", 2, 0), ("alias", 2, 0))
                )
            )
            .Validate();
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);

        // Act
        var resource = first.GetMemory("alias");
        var grown = resource.TryGrow(1, out _);
        resource.Write(65536, [42]);
        var received = new byte[1];
        second.GetMemory("m").Read(65536, received);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(resource).IsSameReferenceAs(original);
            await Assert.That(first.GetMemory("m")).IsSameReferenceAs(original);
            await Assert.That(first.GetMemory("m")).IsSameReferenceAs(first.GetMemory("m"));
            await Assert.That(source.GetMemory("")).IsSameReferenceAs(original);
            await Assert.That(grown).IsTrue();
            await Assert.That(source.GetMemory("m").PageCount).IsEqualTo(2u);
            await Assert.That(received[0]).IsEqualTo((byte)42);
        }
        // Act & Assert
        await Assert.That(() => first.GetMemory("M")).ThrowsExactly<ArgumentException>();
        var exception = await Assert
            .That(() => first.GetMemory(null!))
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
            .That(() => instance.GetMemory(name))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
