using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetMemoryTests
{
    [Test]
    public async Task Core3で複数のメモリを再exportする_importの宣言順に同じ実体を取得でき定義メモリは独立する()
    {
        // Arrange
        var first = new WasmMemory(new WasmLimits(1));
        var second = new WasmMemory(new WasmLimits(1));
        var host = new WasmHostModule("env");
        host.Define("second", second);
        host.Define("first", first);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "first", 2, [0, 1]), ("env", "second", 2, [0, 1])),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("local", 2, 2), ("b", 2, 1), ("a", 2, 0))
        );
        var instance = WasmModule
            .Decode(bytes, WasmSpecVersion.Core3)
            .Validate()
            .Instantiate(new WasmExecutionContext(), [host]);

        // Act
        var exportedFirst = instance.GetMemory("a");
        var exportedSecond = instance.GetMemory("b");
        exportedFirst.Write(0, [17]);
        exportedSecond.Write(0, [42]);
        var firstByte = new byte[1];
        var secondByte = new byte[1];
        var definedByte = new byte[1];
        first.Read(0, firstByte);
        second.Read(0, secondByte);
        instance.GetMemory("local").Read(0, definedByte);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exportedFirst).IsSameReferenceAs(first);
            await Assert.That(exportedSecond).IsSameReferenceAs(second);
            await Assert.That(firstByte[0]).IsEqualTo((byte)17);
            await Assert.That(secondByte[0]).IsEqualTo((byte)42);
            await Assert.That(definedByte[0]).IsEqualTo((byte)0);
        }
    }

    [Test]
    public async Task 別名と再exportから取得する_更新と増大を同じmemoryで共有する()
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Memories((1, 3)),
                    HostLinkingModuleBinary.Exports(("m", 2, 0), ("", 2, 0))
                ),
                WasmSpecVersion.Core2
            )
            .Validate()
            .Instantiate(executionContext, []);
        var original = source.GetMemory("m");
        var host = new WasmHostModule("env");
        host.Define("m", original);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "m", 2, [1, 1, 3])),
                    HostLinkingModuleBinary.Exports(("m", 2, 0), ("alias", 2, 0))
                ),
                WasmSpecVersion.Core2
            )
            .Validate();
        var first = module.Instantiate(executionContext, [host]);
        var second = module.Instantiate(executionContext, [host]);

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
        var exception = await Assert.That(() => first.GetMemory(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    [Arguments("missing")]
    [Arguments("run")]
    [Arguments("")]
    public async Task 指定した種類のexportがない_名前の引数不正として拒否する(string name)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var instance = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B), WasmSpecVersion.Core2)
            .Validate()
            .Instantiate(executionContext, []);

        // Act & Assert
        var exception = await Assert.That(() => instance.GetMemory(name)).ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
