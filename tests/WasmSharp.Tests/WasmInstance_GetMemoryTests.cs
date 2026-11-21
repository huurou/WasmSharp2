using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetMemoryTests
{
    [Test]
    public async Task 両方の幅のメモリを定義する_指定したメモリの初期領域を別名でも読み書きできる()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            // i32は1ページ、i64は最小2ページ、最大2の32乗ページとする。
            HostLinkingModuleBinary.Section(5, [2, 0, 1, 5, 2, 0x80, 0x80, 0x80, 0x80, 0x10]),
            HostLinkingModuleBinary.Exports(("narrow", 2, 0), ("wide", 2, 1), ("alias", 2, 1))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3).Validate();

        // Act
        var instance = module.Instantiate(new WasmExecutionContext(), []);
        var wide = instance.GetMemory("wide");
        var alias = instance.GetMemory("alias");
        var narrow = instance.GetMemory("narrow");
        wide.Write(65535, [17, 42]);
        var wideBytes = new byte[2];
        alias.Read(65535, wideBytes);
        var narrowBytes = new byte[1];
        narrow.Read(65535, narrowBytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(alias).IsSameReferenceAs(wide);
            await Assert.That(wide.Type).IsEqualTo(new WasmMemoryType(WasmValueKind.I64, 2, 1UL << 32));
            await Assert.That(narrow.Type).IsEqualTo(new WasmMemoryType(WasmValueKind.I32, 1));
            await Assert.That(wide.ByteLength).IsEqualTo(131072UL);
            await Assert.That(wideBytes.SequenceEqual(new byte[] { 17, 42 })).IsTrue();
            await Assert.That(narrow.ByteLength).IsEqualTo(65536UL);
            await Assert.That(narrowBytes[0]).IsEqualTo((byte)0);
        }
    }

    [Test]
    public async Task Core3で複数のメモリを再exportする_importの宣言順に同じ実体を取得でき定義メモリは独立する()
    {
        // Arrange
        var first = new WasmMemory(new WasmLimits(1));
        var second = WasmMemory.Create(new WasmMemoryType(WasmValueKind.I64, 0, 1UL << 32));
        second.TryGrow(1, out _);
        var host = new WasmHostModule("env");
        host.Define("second", second);
        host.Define("first", first);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(
                ("env", "first", 2, [0, 1]),
                // 要求上限は2の32乗+1ページで、提供側の最大ページ数より1ページ大きい。
                ("env", "second", 2, [5, 1, 0x81, 0x80, 0x80, 0x80, 0x10])
            ),
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
    [Arguments(WasmSpecVersion.Core2)]
    [Arguments(WasmSpecVersion.Core3)]
    public async Task 別名と再exportから取得する_更新と増大を同じmemoryで共有する(WasmSpecVersion version)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        byte[] type = [version == WasmSpecVersion.Core3 ? (byte)5 : (byte)1, 1, 3];
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Section(5, [1, .. type]),
                    HostLinkingModuleBinary.Exports(("m", 2, 0), ("", 2, 0))
                ),
                version
            )
            .Validate()
            .Instantiate(executionContext, []);
        var original = source.GetMemory("m");
        var host = new WasmHostModule("env");
        host.Define("m", original);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "m", 2, type)),
                    HostLinkingModuleBinary.Exports(("m", 2, 0), ("alias", 2, 0))
                ),
                version
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
