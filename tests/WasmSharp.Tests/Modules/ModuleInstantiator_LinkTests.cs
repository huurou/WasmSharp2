using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Modules.ExternalValues;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ModuleInstantiator_LinkTests
{
    [Test]
    [Arguments(false, 0, false)]
    [Arguments(false, 1, false)]
    [Arguments(false, 2, false)]
    [Arguments(false, 3, true)]
    [Arguments(false, 4, true)]
    [Arguments(true, 0, false)]
    [Arguments(true, 1, false)]
    [Arguments(true, 2, false)]
    [Arguments(true, 3, true)]
    [Arguments(true, 4, true)]
    [Arguments(true, 5, false)]
    public async Task 現在サイズと最大値と参照型を照合する_適合時だけ同じ実体を接続する(
        bool table,
        int scenario,
        bool accepted
    )
    {
        // Arrange
        uint? maximum =
            scenario is 1 or 4 ? null
            : scenario == 2 ? 4u
            : 3u;
        var memory = new WasmMemory(new(0, maximum));
        var resource = new WasmTable(
            scenario == 5 ? WasmValueKind.ExternRef : WasmValueKind.FuncRef,
            new(0, maximum)
        );
        if (scenario != 0)
        {
            memory.TryGrow(2, out _);
            resource.TryGrow(
                2,
                scenario == 5 ? WasmValue.FromExternRef(null) : WasmValue.FromFuncRef(null),
                out _
            );
        }
        var limits = HostLinkingModuleBinary.Limits(2, scenario == 4 ? null : 3u);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(
                        ("", "", table ? (byte)1 : (byte)2, table ? [0x70, .. limits] : limits)
                    )
                )
            )
            .Validate();
        var host = new WasmHostModule();
        if (table)
        {
            host.Define("", resource);
        }
        else
        {
            host.Define("", memory);
        }
        var imports = new WasmImports();
        imports.Add(host);

        if (accepted)
        {
            // Act
            var linked = ModuleInstantiator.Link(module, imports);

            // Assert
            var identical = table
                ? ReferenceEquals(((TableExternalValue)linked[0]).Value, resource)
                : ReferenceEquals(((MemoryExternalValue)linked[0]).Value, memory);
            await Assert.That(identical).IsTrue();
        }
        else
        {
            // Act & Assert
            var exception = await Assert
                .That(() => ModuleInstantiator.Link(module, imports))
                .ThrowsExactly<WasmInstantiateException>();
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
        }
        await Assert.That(memory.PageCount).IsEqualTo(scenario == 0 ? 0u : 2u);
        await Assert.That(resource.Count).IsEqualTo(scenario == 0 ? 0u : 2u);
    }

    [Test]
    public async Task 同名importと余分な提供がある_宣言順に同一実体を接続し実行しない()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7E])),
                    HostLinkingModuleBinary.Imports(
                        ("", "g", 3, [0x7F, 1]),
                        ("", "f", 0, [0]),
                        ("", "f", 0, [0]),
                        ("", "g", 3, [0x7F, 1])
                    )
                )
            )
            .Validate();
        var calls = 0;
        var function = WasmFunction.CreateHost(
            new([WasmValueKind.I32], [WasmValueKind.I64]),
            _ =>
            {
                calls++;
                return new([]);
            }
        );
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(7));
        var host = new WasmHostModule();
        host.Define("f", function);
        host.Define("g", global);
        host.Define("unused", WasmFunction.CreateHost(new([], []), _ => throw new Exception()));
        var imports = new WasmImports();
        imports.Add(host);
        imports.Add(new WasmHostModule("unused"));

        // Act
        var linked = ModuleInstantiator.Link(module, imports);
        host.Define("later", new WasmMemory(new(0)));
        imports.Add(new WasmHostModule("later"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(linked.Length).IsEqualTo(4);
            await Assert
                .That(ReferenceEquals(((FunctionExternalValue)linked[1]).Value, function))
                .IsTrue();
            await Assert.That(ReferenceEquals(linked[1], linked[2])).IsTrue();
            await Assert
                .That(ReferenceEquals(((GlobalExternalValue)linked[0]).Value, global))
                .IsTrue();
            await Assert.That(ReferenceEquals(linked[0], linked[3])).IsTrue();
            await Assert.That(calls).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task 同名関数importの型列が異なる_後続宣言を個別照合して拒否する(
        bool results,
        bool count
    )
    {
        // Arrange
        byte[] first = [0x7F, 0x7E];
        byte[] second = count ? [0x7F] : [0x7E, 0x7F];
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        results ? ([], first) : (first, []),
                        results ? ([], second) : (second, [])
                    ),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0]), ("env", "f", 0, [1]))
                )
            )
            .Validate();
        var host = new WasmHostModule("env");
        host.Define(
            "f",
            WasmFunction.CreateHost(
                new(
                    results ? [] : [WasmValueKind.I32, WasmValueKind.I64],
                    results ? [WasmValueKind.I32, WasmValueKind.I64] : []
                ),
                _ => new([])
            )
        );
        var imports = new WasmImports();
        imports.Add(host);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
            await Assert.That(exception.ImportOrdinal).IsEqualTo(1);
            await Assert.That(exception.ModuleName).IsEqualTo("env");
            await Assert.That(exception.ImportName).IsEqualTo("f");
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Instantiate, module.Imports[1].ByteOffset, null, 2)
                );
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Globalの値型または可変性が異なる_変換せず拒否する(bool mutable)
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(
                        ("env", "g", 3, [mutable ? (byte)0x7F : (byte)0x7E, 0])
                    )
                )
            )
            .Validate();
        var global = new WasmGlobal(new(WasmValueKind.I32, mutable), WasmValue.FromI32(42));
        var host = new WasmHostModule("env");
        host.Define("g", global);
        var imports = new WasmImports();
        imports.Add(host);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
        await Assert.That(global.Value.AsI32()).IsEqualTo(42);
    }

    [Test]
    [Arguments((byte)0, WasmExternalKind.Function)]
    [Arguments((byte)1, WasmExternalKind.Table)]
    [Arguments((byte)2, WasmExternalKind.Memory)]
    [Arguments((byte)3, WasmExternalKind.Global)]
    public async Task 提供実体の種類が異なる_要求種類と宣言位置付きで拒否する(
        byte kind,
        WasmExternalKind expected
    )
    {
        // Arrange
        byte[] type = kind switch
        {
            0 => [0],
            1 => [0x70, 0, 0],
            2 => [0, 0],
            _ => [0x7F, 0],
        };
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("", "", kind, type))
                )
            )
            .Validate();
        var host = new WasmHostModule();
        if (kind == 0)
        {
            host.Define("", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        }
        else
        {
            host.Define("", WasmFunction.CreateHost(new([], []), _ => new([])));
        }
        var imports = new WasmImports();
        imports.Add(host);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.KindMismatch);
        await Assert.That(exception.ExpectedKind).IsEqualTo(expected);
        await Assert
            .That(exception.Location)
            .IsEqualTo(new(WasmProcessingStage.Instantiate, module.Imports[0].ByteOffset, null, 2));
    }

    [Test]
    [Arguments("missing", "f")]
    [Arguments("env", "missing")]
    [Arguments("ENV", "f")]
    [Arguments("env", "F")]
    public async Task 提供名が完全一致しない_宣言の識別情報付きで拒否する(
        string moduleName,
        string name
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports((moduleName, name, 0, [0]))
                )
            )
            .Validate();
        var host = new WasmHostModule("env");
        host.Define("f", WasmFunction.CreateHost(new([], []), _ => new([])));
        var imports = new WasmImports();
        imports.Add(host);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.MissingImport);
            await Assert.That(exception.ImportOrdinal).IsEqualTo(0);
            await Assert.That(exception.ModuleName).IsEqualTo(moduleName);
            await Assert.That(exception.ImportName).IsEqualTo(name);
            await Assert.That(exception.ExpectedKind).IsEqualTo(WasmExternalKind.Function);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Instantiate, module.Imports[0].ByteOffset, null, 2)
                );
        }
    }
}
