using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_GetExportsTests
{
    [Test]
    public async Task 四種と別名をexportする_検証の前後で同じ宣言順の名前と種類を返す()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((0x70, 1, null)),
                HostLinkingModuleBinary.Memories((1, null)),
                HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x05, 0x0B])),
                HostLinkingModuleBinary.Exports(
                    ("g", 3, 0),
                    ("f", 0, 0),
                    ("m", 2, 0),
                    ("t", 1, 0),
                    ("F", 0, 0),
                    ("", 3, 0)
                ),
                HostLinkingModuleBinary.Code(([], [0x0B]))
            )
        );
        WasmExportInfo[] expected =
        [
            new("g", WasmExternalKind.Global),
            new("f", WasmExternalKind.Function),
            new("m", WasmExternalKind.Memory),
            new("t", WasmExternalKind.Table),
            new("F", WasmExternalKind.Function),
            new("", WasmExternalKind.Global),
        ];

        // Act
        var beforeValidation = module.GetExports();
        var instance = module.Validate().Instantiate([]);
        var afterValidation = module.GetExports();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(beforeValidation.SequenceEqual(expected)).IsTrue();
            await Assert.That(afterValidation.SequenceEqual(expected)).IsTrue();
            await Assert
                .That(instance.GetFunction("F"))
                .IsSameReferenceAs(instance.GetFunction("f"));
            await Assert
                .That(instance.GetGlobalResource(""))
                .IsSameReferenceAs(instance.GetGlobalResource("g"));
        }
    }

    [Test]
    public async Task Importした四種を再exportする_一覧の名前から提供元と同じ実体を取得できる()
    {
        // Arrange
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Tables((0x70, 1, null)),
                    HostLinkingModuleBinary.Memories((1, null)),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x05, 0x0B])),
                    HostLinkingModuleBinary.Exports(
                        ("f", 0, 0),
                        ("t", 1, 0),
                        ("m", 2, 0),
                        ("g", 3, 0)
                    ),
                    HostLinkingModuleBinary.Code(([], [0x0B]))
                )
            )
            .Validate()
            .Instantiate([]);
        var host = new WasmHostModule("env");
        host.Define("f", source.GetFunction("f"));
        host.Define("t", source.GetTable("t"));
        host.Define("m", source.GetMemory("m"));
        host.Define("g", source.GetGlobalResource("g"));
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "f", 0, [0]),
                        ("env", "t", 1, [0x70, .. HostLinkingModuleBinary.Limits(1)]),
                        ("env", "m", 2, HostLinkingModuleBinary.Limits(1)),
                        ("env", "g", 3, [0x7F, 0x01])
                    ),
                    HostLinkingModuleBinary.Exports(
                        ("memory", 2, 0),
                        ("global", 3, 0),
                        ("table", 1, 0),
                        ("function", 0, 0)
                    )
                )
            )
            .Validate();
        var instance = module.Instantiate([host]);
        WasmExportInfo[] expected =
        [
            new("memory", WasmExternalKind.Memory),
            new("global", WasmExternalKind.Global),
            new("table", WasmExternalKind.Table),
            new("function", WasmExternalKind.Function),
        ];

        // Act
        var exports = module.GetExports();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exports.SequenceEqual(expected)).IsTrue();
            await Assert
                .That(instance.GetMemory(exports[0].Name))
                .IsSameReferenceAs(source.GetMemory("m"));
            await Assert
                .That(instance.GetGlobalResource(exports[1].Name))
                .IsSameReferenceAs(source.GetGlobalResource("g"));
            await Assert
                .That(instance.GetTable(exports[2].Name))
                .IsSameReferenceAs(source.GetTable("t"));
            await Assert
                .That(instance.GetFunction(exports[3].Name))
                .IsSameReferenceAs(source.GetFunction("f"));
        }
    }

    [Test]
    public async Task Exportがない_空の一覧を返す()
    {
        // Arrange
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create());

        // Act
        var exports = module.GetExports();

        // Assert
        await Assert.That(exports).IsEmpty();
    }
}
