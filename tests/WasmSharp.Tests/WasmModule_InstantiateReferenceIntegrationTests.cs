using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments((byte)0x70)]
    [Arguments((byte)0x6F)]
    public async Task Importされたimmutable参照globalを定数式で取得する_定義globalと結果に同じ参照を返す(
        byte kind
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [kind])),
                    HostLinkingModuleBinary.Imports(("env", "g", 3, [kind, 0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Globals((kind, false, [0x23, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(("g", 3, 1), ("run", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x23, 1, 0x0B]))
                )
            )
            .Validate();
        var value =
            kind == 0x70
                ? WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([])))
                : WasmValue.FromExternRef(new object());
        var host = new WasmHostModule("env");
        host.Define("g", new WasmGlobal(new(value.Kind, false), value));

        // Act
        var instance = module.Instantiate([host]);
        var values = instance.GetFunction("run").Invoke([]).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values.SequenceEqual([value])).IsTrue();
            await Assert.That(instance.GetGlobal("g")).IsEqualTo(value);
        }
    }

    [Test]
    public async Task Globalだけで宣言したimport関数を参照する_関数生成前のglobal評価を行わず同じ実体を返す()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x70])),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Globals((0x70, false, [0xD2, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("g", 3, 0)),
                    HostLinkingModuleBinary.Code(([], [0xD2, 0, 0x0B]))
                )
            )
            .Validate();
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var host = new WasmHostModule("env");
        host.Define("f", function);

        // Act
        var instance = module.Instantiate([host]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.GetGlobal("g").AsFuncRef()).IsSameReferenceAs(function);
            await Assert
                .That(instance.GetFunction("run").Invoke([]).Values[0].AsFuncRef())
                .IsSameReferenceAs(function);
        }
    }
}
