using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_GetFunctionTests
{
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
