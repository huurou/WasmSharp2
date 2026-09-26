using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 共有memoryを読んでから別instanceへ再入する_コピーを保持し増大後の現在領域へ書き込む()
    {
        // Arrange
        var memory = new WasmMemory(new(1, 2));
        var table = new WasmTable(WasmValueKind.ExternRef, new(1, 2));
        var reference = new object();
        memory.Write(0, [7]);
        var grown = false;
        var host = new WasmHostModule("env");
        host.Define("m", memory);
        host.Define("t", table);
        host.Define(
            "grow",
            WasmFunction.CreateHost(
                new([], []),
                (instance, arguments) =>
                {
                    grown = instance.GetMemory("m").TryGrow(1, out _);
                    grown &= instance
                        .GetTable("t")
                        .TryGrow(1, WasmValue.FromExternRef(reference), out _);
                    instance.GetMemory("m").Write(0, [9]);
                    return new([]);
                }
            )
        );
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "grow", 0, [0]),
                        ("env", "m", 2, [1, 1, 2]),
                        ("env", "t", 1, [0x6F, 1, 1, 2])
                    ),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("grow", 0, 1), ("m", 2, 0), ("t", 1, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([host]);
        var copy = new byte[1];
        var provider = new WasmHostModule("env");
        provider.Define("m", source.GetMemory("m"));
        provider.Define("t", source.GetTable("t"));
        provider.Define(
            "roundtrip",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    instance.GetMemory("m").Read(0, copy);
                    source.GetFunction("grow").Invoke([]);
                    instance.GetMemory("m").Write(65536, [42]);
                    instance.GetTable("t").Set(0, instance.GetTable("t").Get(1));
                    return new([]);
                }
            )
        );
        var destination = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "roundtrip", 0, [0]),
                        ("env", "m", 2, [1, 1, 2]),
                        ("env", "t", 1, [0x6F, 1, 1, 2])
                    ),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("m", 2, 0), ("t", 1, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([provider], new(4));

        // Act
        destination.GetFunction("run").Invoke([]);
        var current = new byte[1];
        var added = new byte[1];
        source.GetMemory("m").Read(0, current);
        memory.Read(65536, added);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(grown).IsTrue();
            await Assert.That(copy[0]).IsEqualTo((byte)7);
            await Assert.That(current[0]).IsEqualTo((byte)9);
            await Assert.That(added[0]).IsEqualTo((byte)42);
            await Assert.That(destination.GetMemory("m").PageCount).IsEqualTo(2u);
            await Assert.That(source.GetTable("t").Count).IsEqualTo(2u);
            await Assert.That(table.Get(0).AsExternRef()).IsSameReferenceAs(reference);
            await Assert
                .That(destination.GetTable("t").Get(1).AsExternRef())
                .IsSameReferenceAs(reference);
        }
    }
}
