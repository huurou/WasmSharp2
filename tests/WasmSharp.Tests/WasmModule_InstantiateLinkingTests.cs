using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 巨大table定義とstartを持つ_全リンク成功後だけ保持上限を返してstartを実行しない(
        bool provideGlobal
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "f", 0, [0]),
                        ("env", "g", 3, [0x7F, 0])
                    ),
                    HostLinkingModuleBinary.Tables((0x70, uint.MaxValue, null)),
                    HostLinkingModuleBinary.Start(0)
                )
            )
            .Validate();
        var host = new WasmHostModule("env");
        var calls = 0;
        host.Define(
            "f",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );
        if (provideGlobal)
        {
            host.Define("g", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        }

        // Act & Assert
        if (provideGlobal)
        {
            var exception = await Assert
                .That(() => module.Instantiate([host]))
                .ThrowsExactly<WasmImplementationLimitException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Reason)
                    .IsEqualTo(WasmImplementationLimitReason.CollectionSize);
                await Assert.That(exception.Limit).IsEqualTo(Array.MaxLength);
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(WasmProcessingStage.Instantiate, module.Tables[0].ByteOffset, null, 4)
                    );
                await Assert.That(calls).IsEqualTo(0);
            }
        }
        else
        {
            var exception = await Assert
                .That(() => module.Instantiate([host]))
                .ThrowsExactly<WasmInstantiateException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.MissingImport);
                await Assert.That(exception.ImportName).IsEqualTo("g");
                await Assert.That(calls).IsEqualTo(0);
            }
        }
    }

    [Test]
    public async Task ホストstartを持つ_Instantiateごとに一回実行して別のinstanceを返す()
    {
        // Arrange
        var calls = 0;
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "start", 0, [0])),
                    HostLinkingModuleBinary.Start(0)
                )
            )
            .Validate();
        var host = new WasmHostModule("env");
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );

        // Act
        var first = module.Instantiate([host], new(1));
        var firstCalls = calls;
        var second = module.Instantiate([host], new(1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstCalls).IsEqualTo(1);
            await Assert.That(calls).IsEqualTo(2);
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
        }
    }

    [Test]
    public async Task 提供登録が重複する_リンク前に契約違反で拒否する()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("missing", "missing", 3, [0x7F, 0]))
                )
            )
            .Validate();
        var first = new WasmHostModule("env");
        var second = new WasmHostModule("env");
        first.Define("f", WasmFunction.CreateHost(new([], []), _ => new([])));
        second.Define("f", new WasmMemory(new(0)));

        // Act & Assert
        await Assert
            .That(() => module.Instantiate([first, second]))
            .ThrowsExactly<ArgumentException>();
        await Assert
            .That(() => module.Instantiate((WasmImports)null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => module.Instantiate([null!])).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task 未検証で提供登録を指定する_解決やcallbackより前に拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Imports(("missing", "g", 3, [0x7F, 0]))
            )
        );

        // Act & Assert
        await Assert
            .That(() => module.Instantiate(new WasmImports()))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments((byte)0x7F)]
    [Arguments((byte)0x7E)]
    [Arguments((byte)0x7D)]
    [Arguments((byte)0x7C)]
    [Arguments((byte)0x7B)]
    [Arguments((byte)0x70)]
    [Arguments((byte)0x6F)]
    public async Task ImportedGlobalで定義を初期化する_7型の値と参照を保ち種類別添字で接続する(
        byte kind
    )
    {
        // Arrange
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var value = kind switch
        {
            0x7F => WasmValue.FromI32(-42),
            0x7E => WasmValue.FromI64(long.MinValue),
            0x7D => WasmValue.FromF32Bits(0xFFC00042),
            0x7C => WasmValue.FromF64Bits(0xFFF8000000000042),
            0x7B => WasmValue.FromV128(0xFEDCBA9876543210, 0x0123456789ABCDEF),
            0x70 => WasmValue.FromFuncRef(function),
            _ => WasmValue.FromExternRef(new object()),
        };
        var global = new WasmGlobal(new(value.Kind, false), value);
        var shared = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(10));
        var memory = new WasmMemory(new(1, 2));
        var table = new WasmTable(WasmValueKind.FuncRef, new(1, 2));
        var host = new WasmHostModule("env");
        host.Define("value", global);
        host.Define("shared", shared);
        host.Define("f", function);
        host.Define("m", memory);
        host.Define("t", table);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "shared", 3, [0x7F, 1]),
                        ("env", "t", 1, [0x70, 0, 1]),
                        ("env", "f", 0, [0]),
                        ("env", "m", 2, [0, 1]),
                        ("env", "value", 3, [kind, 0])
                    ),
                    HostLinkingModuleBinary.Tables((0x6F, 1, null)),
                    HostLinkingModuleBinary.Globals((kind, true, [0x23, 1, 0x0B])),
                    HostLinkingModuleBinary.Exports(
                        ("g", 3, 2),
                        ("shared", 3, 0),
                        ("m", 2, 0),
                        ("t", 1, 0),
                        ("defined", 1, 1)
                    )
                )
            )
            .Validate();

        // Act
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);
        shared.Value = WasmValue.FromI32(77);
        first.GetTable("t").Set(0, WasmValue.FromFuncRef(function));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetGlobal("g")).IsEqualTo(value);
            await Assert.That(second.GetGlobal("g")).IsEqualTo(value);
            await Assert.That(first.GetGlobal("shared").AsI32()).IsEqualTo(77);
            await Assert.That(second.GetGlobal("shared").AsI32()).IsEqualTo(77);
            await Assert.That(first.GetMemory("m")).IsSameReferenceAs(memory);
            await Assert.That(second.GetMemory("m")).IsSameReferenceAs(memory);
            await Assert.That(second.GetTable("t").Get(0).AsFuncRef()).IsSameReferenceAs(function);
            await Assert.That(first.GetTable("defined").Get(0).AsExternRef()).IsNull();
            if (kind == 0x70)
            {
                await Assert.That(first.GetGlobal("g").AsFuncRef()).IsSameReferenceAs(function);
            }
            if (kind == 0x6F)
            {
                await Assert
                    .That(first.GetGlobal("g").AsExternRef())
                    .IsSameReferenceAs(value.AsExternRef());
            }
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 定義リソースを持つ_初期化してinstanceごとに独立させる(bool useStream)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Tables((0x70, 1, 2), (0x6F, 1, null)),
            HostLinkingModuleBinary.Memories((1, 2)),
            HostLinkingModuleBinary.Globals(
                (0x7F, true, [0x41, 42, 0x0B]),
                (0x7E, false, [0x42, 0x7F, 0x0B]),
                (0x7D, false, [0x43, 0x42, 0, 0xC0, 0xFF, 0x0B]),
                (0x7C, false, [0x44, 0x42, 0, 0, 0, 0, 0, 0xF8, 0xFF, 0x0B])
            ),
            HostLinkingModuleBinary.Exports(
                ("g", 3, 0),
                ("i64", 3, 1),
                ("f32", 3, 2),
                ("f64", 3, 3),
                ("m", 2, 0),
                ("t", 1, 0),
                ("extern", 1, 1)
            )
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        var module = (useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes)).Validate();

        // Act
        var first = module.Instantiate([]);
        var second = module.Instantiate(new WasmImports());
        var initial = new byte[4];
        first.GetMemory("m").Read(65532, initial);
        first.GetMemory("m").Write(0, [42]);
        first.GetMemory("m").TryGrow(1, out _);
        first.GetTable("t").TryGrow(1, WasmValue.FromFuncRef(null), out _);
        var reference = new object();
        first.GetTable("extern").Set(0, WasmValue.FromExternRef(reference));
        var other = new byte[1];
        second.GetMemory("m").Read(0, other);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetGlobal("g").AsI32()).IsEqualTo(42);
            await Assert.That(first.GetGlobal("i64").AsI64()).IsEqualTo(-1L);
            await Assert.That(first.GetGlobal("f32").AsF32Bits()).IsEqualTo(0xFFC00042u);
            await Assert.That(first.GetGlobal("f64").AsF64Bits()).IsEqualTo(0xFFF8000000000042UL);
            await Assert.That(first.GetGlobalResource("i64").Type.IsMutable).IsFalse();
            await Assert.That(initial).IsEquivalentTo(new byte[4]);
            await Assert.That(other[0]).IsEqualTo((byte)0);
            await Assert.That(first.GetMemory("m").PageCount).IsEqualTo(2u);
            await Assert.That(first.GetMemory("m").MaximumPages).IsEqualTo(2u);
            await Assert.That(second.GetMemory("m").PageCount).IsEqualTo(1u);
            await Assert.That(first.GetTable("t").MaximumElements).IsEqualTo(2u);
            await Assert.That(first.GetTable("extern").MaximumElements).IsNull();
            await Assert.That(second.GetTable("t").Count).IsEqualTo(1u);
            await Assert.That(second.GetTable("t").Get(0).AsFuncRef()).IsNull();
            await Assert.That(second.GetTable("extern").Get(0).AsExternRef()).IsNull();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Importと定義関数を持つ_型とmodule全体添字を保って構築する(bool useSpan)
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Exports(("imported", 0, 0), ("defined", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x41, 42, 0x0B]))
                )
            )
            .Validate();
        var calls = 0;
        var function = WasmFunction.CreateHost(
            new([], []),
            _ =>
            {
                calls++;
                return new([]);
            }
        );
        var host = new WasmHostModule("env");
        host.Define("f", function);
        var imports = new WasmImports();
        imports.Add(host);
        var options = new WasmExecutionOptions(7);

        // Act
        var instance = useSpan
            ? module.Instantiate([host], options)
            : module.Instantiate(imports, options);
        var result = instance.GetFunction("defined").Invoke([]);

        // Assert
        await Assert.That(instance.GetFunction("imported")).IsSameReferenceAs(function);
        await Assert.That(instance.ExecutionOptions).IsSameReferenceAs(options);
        await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
        await Assert.That(calls).IsEqualTo(0);
    }
}
