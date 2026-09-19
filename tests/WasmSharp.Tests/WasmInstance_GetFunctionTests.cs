using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetFunctionTests
{
    [Test]
    public async Task Importを持つ定義関数を再exportする_元instanceと関数添字を維持する()
    {
        // Arrange
        var host = new WasmHostModule("env");
        host.Define("f", WasmFunction.CreateHost(new([], []), _ => new([])));
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Exports(("run", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x41, 42, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([host]);
        var original = source.GetFunction("run");
        var provider = new WasmHostModule("source");
        provider.Define("run", original);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("source", "run", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("", 0, 0), ("own", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x41, 7, 0x0B]))
                )
            )
            .Validate();

        // Act
        var first = module.Instantiate([provider]);
        var second = module.Instantiate([provider]);
        var reexported = first.GetFunction("run");
        var result = reexported.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reexported).IsSameReferenceAs(original);
            await Assert.That(first.GetFunction("")).IsSameReferenceAs(original);
            await Assert.That(second.GetFunction("run")).IsSameReferenceAs(original);
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(first.GetFunction("own").Invoke([]).Values[0].AsI32()).IsEqualTo(7);
            await Assert
                .That(ReferenceEquals(first.GetFunction("own"), second.GetFunction("own")))
                .IsFalse();
            var defined = (WasmSharp.Execution.WasmDefinedFunction)reexported;
            await Assert.That(defined.Instance).IsSameReferenceAs(source);
            await Assert.That(defined.FunctionIndex).IsEqualTo(1u);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ホスト関数を別名と再exportで取得する_取得元へ固定せず同じ実体を返す(
        bool receivesInstance
    )
    {
        // Arrange
        var type = new WasmFunctionType([], []);
        var function = receivesInstance
            ? WasmFunction.CreateHost(type, (_, _) => new([]))
            : WasmFunction.CreateHost(type, _ => new([]));
        var host = new WasmHostModule("env");
        host.Define("f", function);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                    HostLinkingModuleBinary.Exports(("f", 0, 0), ("alias", 0, 0))
                )
            )
            .Validate();
        var source = module.Instantiate([host]);
        var provider = new WasmHostModule("env");
        provider.Define("f", source.GetFunction("f"));

        // Act
        var destination = module.Instantiate([provider]);

        // Assert
        await Assert.That(destination.GetFunction("f")).IsSameReferenceAs(function);
        await Assert.That(destination.GetFunction("alias")).IsSameReferenceAs(function);
        await Assert.That(destination.GetFunction("f").Type).IsSameReferenceAs(type);
    }

    [Test]
    public async Task 名前がnullである_名前のnull引数例外で拒否する()
    {
        // Arrange
        var instance = WasmModule
            .Decode(HostLinkingModuleBinary.Create())
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetFunction(null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    public async Task 名前が別種類のexportである_名前の引数例外で拒否する()
    {
        // Arrange
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(("g", 3, 0))
                )
            )
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetFunction("g"))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    public async Task 複数の名前とインスタンスから関数を取得する_型と関数実体の同一性を保つ()
    {
        // Arrange
        var module = WasmModule
            .Decode(
                ConstantModuleBinary.Create(
                    [(0x7F, [0x41, 0x01, 0x0B]), (0x7E, [0x42, 0x02, 0x0B])],
                    [("run", 1), ("alias", 1), ("Run", 0), ("", 0)]
                )
            )
            .Validate();
        var first = module.Instantiate([]);
        var second = module.Instantiate([new WasmHostModule()]);

        // Act
        var function = first.GetFunction("run");
        var repeated = first.GetFunction("run");
        var alias = first.GetFunction("alias");
        var otherFunction = first.GetFunction("Run");
        var otherInstance = second.GetFunction("run");
        var result = function.Invoke([]);
        var aliasResult = alias.Invoke([]);
        var otherFunctionResult = otherFunction.Invoke([]);
        var otherInstanceResult = otherInstance.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(function, repeated)).IsTrue();
            await Assert.That(ReferenceEquals(function, alias)).IsTrue();
            await Assert.That(ReferenceEquals(function, otherFunction)).IsFalse();
            await Assert.That(ReferenceEquals(function, otherInstance)).IsFalse();
            await Assert.That(function.Type.Parameters).IsEmpty();
            await Assert.That(function.Type.Results.Single()).IsEqualTo(WasmValueKind.I64);
            await Assert.That(otherFunction.Type.Results.Single()).IsEqualTo(WasmValueKind.I32);
            await Assert.That(ReferenceEquals(first.GetFunction(""), otherFunction)).IsTrue();
            await Assert.That(result.Values.Single().AsI64()).IsEqualTo(2L);
            await Assert.That(aliasResult.Values.Single().AsI64()).IsEqualTo(2L);
            await Assert.That(otherFunctionResult.Values.Single().AsI32()).IsEqualTo(1);
            await Assert.That(otherInstanceResult.Values.Single().AsI64()).IsEqualTo(2L);
        }
    }

    [Test]
    [Arguments("missing")]
    [Arguments("RUN")]
    public async Task 関数名が存在しない_名前の引数不正として拒否する(string name)
    {
        // Arrange
        var instance = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate()
            .Instantiate([]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetFunction(name))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
