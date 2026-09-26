using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Start中のcallbackからexportを使う_定義資源と関数は初期化済みで再入できる(
        bool definedStart
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "start", 0, [0])),
                    HostLinkingModuleBinary.Functions(1, 0),
                    HostLinkingModuleBinary.Tables((0x6F, 1, null)),
                    HostLinkingModuleBinary.Memories((1, null)),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x2A, 0x0B])),
                    HostLinkingModuleBinary.Exports(
                        ("read", 0, 1),
                        ("memory", 2, 0),
                        ("table", 1, 0),
                        ("value", 3, 0)
                    ),
                    HostLinkingModuleBinary.Start(definedStart ? 2U : 0U),
                    HostLinkingModuleBinary.Code(([], [0x23, 0x00, 0x0B]), ([], [0x10, 0x00, 0x0B]))
                )
            )
            .Validate();
        var host = new WasmHostModule("env");
        WasmInstance? received = null;
        var calls = 0;
        var value = 0;
        byte[] initialMemory = [255];
        object? initialElement = new();
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    received = instance;
                    calls++;
                    value = instance.GetFunction("read").Invoke([]).Values[0].AsI32();
                    instance.GetMemory("memory").Read(0, initialMemory);
                    initialElement = instance.GetTable("table").Get(0).AsExternRef();
                    instance.GetMemory("memory").Write(0, [9]);
                    return new([]);
                }
            )
        );
        var imports = new WasmImports();
        imports.Add(host);

        // Act
        var result = module.Instantiate(imports, new(3));
        byte[] written = [0];
        result.GetMemory("memory").Read(0, written);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(received, result)).IsTrue();
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(value).IsEqualTo(42);
            await Assert.That(initialMemory[0]).IsEqualTo((byte)0);
            await Assert.That(initialElement).IsNull();
            await Assert.That(written[0]).IsEqualTo((byte)9);
        }
    }

    [Test]
    public async Task Importした定義関数がstartになる_元の資源とcallbackのinstanceを保ちstart所有者の上限を使う()
    {
        // Arrange
        WasmInstance? received = null;
        var host = new WasmHostModule("env");
        host.Define(
            "observe",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    received = instance;
                    return new([]);
                }
            )
        );
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "observe", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x00, 0x0B])),
                    HostLinkingModuleBinary.Exports(("start", 0, 1), ("value", 3, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0x00, 0x41, 0x07, 0x24, 0x00, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([host], new(1));
        var provider = new WasmHostModule("source");
        provider.Define("start", source.GetFunction("start"));
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("source", "start", 0, [0])),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x2A, 0x0B])),
                    HostLinkingModuleBinary.Exports(("start", 0, 0), ("value", 3, 0)),
                    HostLinkingModuleBinary.Start(0)
                )
            )
            .Validate();

        // Act
        var owner = module.Instantiate([provider], new(2));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(received, source)).IsTrue();
            await Assert
                .That(ReferenceEquals(owner.GetFunction("start"), source.GetFunction("start")))
                .IsTrue();
            await Assert.That(source.GetGlobal("value").AsI32()).IsEqualTo(7);
            await Assert.That(owner.GetGlobal("value").AsI32()).IsEqualTo(42);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Startが副作用と参照保存の後に失敗する_instanceを返さず保存参照の操作と完了済み更新を維持する(
        bool hostFailure
    )
    {
        // Arrange
        var expected = new InvalidOperationException("ホスト処理の失敗");
        var shared = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var element = new object();
        WasmInstance? savedInstance = null;
        WasmFunction? savedFunction = null;
        WasmMemory? savedMemory = null;
        WasmTable? savedTable = null;
        WasmGlobal? savedGlobal = null;
        WasmInstance? returned = null;
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define("shared", shared);
        host.Define(
            "save",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    calls++;
                    savedInstance = instance;
                    savedFunction = instance.GetFunction("read");
                    savedMemory = instance.GetMemory("memory");
                    savedTable = instance.GetTable("table");
                    savedGlobal = instance.GetGlobalResource("value");
                    savedMemory.Write(0, [9]);
                    savedTable.Set(0, WasmValue.FromExternRef(element));
                    if (hostFailure)
                    {
                        throw expected;
                    }
                    return new([]);
                }
            )
        );
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "save", 0, [0]),
                        ("env", "shared", 3, [0x7F, 1])
                    ),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Tables((0x6F, 1, null)),
                    HostLinkingModuleBinary.Memories((1, null)),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x2A, 0x0B])),
                    HostLinkingModuleBinary.Exports(
                        ("read", 0, 2),
                        ("memory", 2, 0),
                        ("table", 1, 0),
                        ("value", 3, 1)
                    ),
                    HostLinkingModuleBinary.Start(1),
                    HostLinkingModuleBinary.Code(
                        ([], [0x41, 0x07, 0x24, 0x00, 0x10, 0x00, 0x00, 0x0B]),
                        ([], [0x23, 0x01, 0x0B])
                    )
                )
            )
            .Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => returned = module.Instantiate([host], new(2)))
            .Throws<Exception>();
        await Assert.That(savedInstance).IsNotNull();

        // Act
        var initialValue = savedFunction!.Invoke([]).Values[0].AsI32();
        savedGlobal!.Value = WasmValue.FromI32(43);
        var updatedValue = savedInstance!.GetFunction("read").Invoke([]).Values[0].AsI32();
        byte[] written = [0];
        savedMemory!.Read(0, written);
        savedMemory.Write(0, [10]);
        byte[] updated = [0];
        savedInstance.GetMemory("memory").Read(0, updated);

        // Assert
        using (Assert.Multiple())
        {
            if (hostFailure)
            {
                await Assert.That(ReferenceEquals(exception, expected)).IsTrue();
            }
            else
            {
                await Assert.That(exception).IsTypeOf<WasmTrapException>();
                await Assert
                    .That((exception as WasmTrapException)?.Location?.Stage)
                    .IsEqualTo(WasmProcessingStage.Instantiate);
            }
            await Assert.That(returned).IsNull();
            await Assert.That(shared.Value.AsI32()).IsEqualTo(7);
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(initialValue).IsEqualTo(42);
            await Assert.That(updatedValue).IsEqualTo(43);
            await Assert.That(written[0]).IsEqualTo((byte)9);
            await Assert.That(updated[0]).IsEqualTo((byte)10);
            await Assert.That(ReferenceEquals(savedTable!.Get(0).AsExternRef(), element)).IsTrue();
        }
    }

    [Test]
    public async Task Startの再帰が上限に達する_Instantiate段階の失敗後に新しい上限で再実行できる()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Start(0),
                    HostLinkingModuleBinary.Code(([], [0x10, 0x00, 0x0B]))
                )
            )
            .Validate();

        // Act & Assert
        var first = await Assert
            .That(() => module.Instantiate([], new(2)))
            .ThrowsExactly<WasmExhaustionException>();
        var second = await Assert
            .That(() => module.Instantiate([], new(3)))
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(first!.Limit).IsEqualTo(2);
            await Assert.That(second!.Limit).IsEqualTo(3);
            await Assert.That(first.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(second.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(first.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
            await Assert.That(second.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
        }
    }

    [Test]
    public async Task 実行中のcallbackからInstantiateする_外側の上限を共有してstart終了後も値を保って継続する()
    {
        // Arrange
        var calls = 0;
        var innerHost = new WasmHostModule("inner");
        innerHost.Define(
            "observe",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );
        var innerModule = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("inner", "observe", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Start(1),
                    HostLinkingModuleBinary.Code(([], [0x10, 0x00, 0x0B]))
                )
            )
            .Validate();
        var outerHost = new WasmHostModule("outer");
        outerHost.Define(
            "create",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    innerModule.Instantiate([innerHost], new(1));
                    innerModule.Instantiate([innerHost], new(1));
                    return new([]);
                }
            )
        );
        var outer = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("outer", "create", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Exports(("run", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x41, 0x2A, 0x10, 0x00, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([outerHost], new(4));

        // Act
        var result = outer.GetFunction("run").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(calls).IsEqualTo(2);
        }
    }
}
