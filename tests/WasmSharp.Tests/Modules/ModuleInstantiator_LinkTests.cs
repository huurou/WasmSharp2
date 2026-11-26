using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ModuleInstantiator_LinkTests
{
    [Test]
    public async Task 関数の要求型と提供型が異なる_引数と結果を宣言順で診断へ含める()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F, 0x7E], [0x7D, 0x7C])),
                    HostLinkingModuleBinary.Imports(("env", "function", 0, [0]))
                ),
                WasmSpecVersion.Core2
            )
            .Validate();
        var host = new WasmHostModule("env");
        host.Define(
            "function",
            WasmFunction.CreateHost(
                new([WasmValueKind.I64, WasmValueKind.I32], [WasmValueKind.F64, WasmValueKind.F32]),
                (_, _) => new([])
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
            await Assert.That(exception.Message).Contains("要求型 [I32, I64] -> [F32, F64]");
            await Assert.That(exception.Message).Contains("提供型は [I64, I32] -> [F64, F32]");
        }
    }

    [Test]
    public async Task Core2の関数の引数が16個を超える_引数と結果をすべてリンク診断に表示する()
    {
        // Arrange
        byte[] parameters = [.. Enumerable.Repeat((byte)0x7F, 17)];
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types((parameters, [0x7E])),
                    HostLinkingModuleBinary.Imports(("env", "function", 0, [0]))
                ),
                WasmSpecVersion.Core2
            )
            .Validate();
        var host = new WasmHostModule("env");
        host.Define("function", WasmFunction.CreateHost(new WasmFunctionType([], []), (_, _) => new WasmResults([])));
        var imports = new WasmImports();
        imports.Add(host);
        var expectedParameters = string.Join(", ", Enumerable.Repeat("I32", 17));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        await Assert.That(exception!.Message).Contains($"要求型 [{expectedParameters}] -> [I64]");
    }

    [Test]
    [Arguments(4, "I32 I32 I32 I32", "I32 I32 I32 I32", "F64")]
    [Arguments(5, "I32 I32 …", "I32 I32 I32 …", "…")]
    public async Task 型付き関数参照を繰り返す_リンク診断は引数と結果の入れ子を通じて16個までの値型を表示する(
        int nestedParameterCount,
        string lastRequiredParameters,
        string lastProvidedParameters,
        string providedResults
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Section(
                        1,
                        [
                            2,
                            0x60,
                            (byte)nestedParameterCount,
                            .. Enumerable.Repeat((byte)0x7F, nestedParameterCount),
                            0,
                            0x60,
                            1,
                            0x7C,
                            3,
                            0x64,
                            0,
                            0x64,
                            0,
                            0x64,
                            0, // f64 -> (ref 0) (ref 0) (ref 0)
                        ]
                    ),
                    HostLinkingModuleBinary.Imports(("env", "function", 0, [1]))
                ),
                WasmSpecVersion.Core3
            )
            .Validate();
        var reference = WasmValueType.FunctionReference(module.FunctionTypeAt(0), false);
        var provided = WasmFunctionType.Create([reference, reference, reference], [WasmValueKind.F64]);
        var host = new WasmHostModule("env");
        host.Define("function", WasmFunction.CreateHost(provided, (_, _) => new WasmResults([])));
        var imports = new WasmImports();
        imports.Add(host);
        var parameters = string.Join(" ", Enumerable.Repeat("I32", nestedParameterCount));
        var fullReference = $"(ref (func (param {parameters}) (result )))";
        var lastRequired = $"(ref (func (param {lastRequiredParameters}) (result )))";
        var lastProvided = $"(ref (func (param {lastProvidedParameters}) (result )))";

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message)
                .Contains($"要求型 [F64] -> [{fullReference}, {fullReference}, {lastRequired}]");
            await Assert
                .That(exception.Message)
                .Contains($"提供型は [{fullReference}, {fullReference}, {lastProvided}] -> [{providedResults}]");
        }
    }

    [Test]
    public async Task Tableの要求型と提供型が異なる_参照型と現在サイズと上限を診断へ含める()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Imports(("env", "table", 1, [0x70, 1, 2, 3]))),
                WasmSpecVersion.Core2
            )
            .Validate();
        var table = new WasmTable(WasmValueKind.ExternRef, new(0, 7));
        table.TryGrow(4, WasmValue.FromExternRef(null), out _);
        var host = new WasmHostModule("env");
        host.Define("table", table);
        var imports = new WasmImports();
        imports.Add(host);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.TypeMismatch);
            await Assert.That(exception.Message).Contains("要求型 FuncRef");
            await Assert.That(exception.Message).Contains("Minimum = 2, Maximum = 3");
            await Assert.That(exception.Message).Contains("提供型は ExternRef");
            await Assert.That(exception.Message).Contains("Minimum = 4, Maximum = 7");
        }
    }

    [Test]
    public async Task 関数importへglobalを提供する_要求と提供の種類を診断へ含める()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "value", 0, [0]))
                ),
                WasmSpecVersion.Core2
            )
            .Validate();
        var host = new WasmHostModule("env");
        host.Define("value", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        var imports = new WasmImports();
        imports.Add(host);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleInstantiator.Link(module, imports))
            .ThrowsExactly<WasmInstantiateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmInstantiateReason.KindMismatch);
            await Assert.That(exception.Message).Contains("要求種類 Function");
            await Assert.That(exception.Message).Contains("提供種類は Global");
        }
    }
}
