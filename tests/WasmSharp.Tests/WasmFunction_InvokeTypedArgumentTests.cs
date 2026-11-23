using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 型付き参照を結果に持つホスト関数を呼ぶ_参照先のシグネチャとnull可否を検査する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var referenceType = WasmValueType.FunctionReference(new WasmFunctionType([], []), isNullable: false);
        var factoryType = WasmFunctionType.Create([], [referenceType]);
        var target = WasmFunction.CreateHost(new WasmFunctionType([], []), (_, _) => new WasmResults([]));
        var supplied = WasmValue.FromFuncRef(target);
        var factory = WasmFunction.CreateHost(factoryType, (_, _) => new WasmResults([supplied]));

        // Act
        var result = factory.Invoke(context, []);

        // Assert
        await Assert.That(result.Values[0].AsFuncRef()).IsSameReferenceAs(target);

        // Act & Assert
        supplied = WasmValue.FromFuncRef(factory);
        await Assert.That(() => factory.Invoke(context, [])).ThrowsExactly<InvalidOperationException>();
        supplied = WasmValue.FromFuncRef(null);
        await Assert.That(() => factory.Invoke(context, [])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task 型付き参照の引数を渡す_同じシグネチャの関数を返しnullと別シグネチャを拒否する()
    {
        // Arrange
        var context = new WasmExecutionContext();
        var module = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x60, 0, 1, 0x7F, 0x60, 1, 0x64, 0, 1, 0x64, 0]),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Exports(("identity", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x0B]))
        );
        var identity = WasmModule.Decode(module).Validate().Instantiate(context, []).GetFunction("identity");
        var answer = WasmFunction.CreateHost(new WasmFunctionType([], [WasmValueKind.I32]), (_, _) =>
            new WasmResults([WasmValue.FromI32(42)])
        );

        // Act
        var result = identity.Invoke(context, [WasmValue.FromFuncRef(answer)]);

        // Assert
        await Assert.That(result.Values[0].AsFuncRef()).IsSameReferenceAs(answer);

        // Act & Assert
        await Assert.That(() => identity.Invoke(context, [WasmValue.FromFuncRef(null)])).ThrowsExactly<ArgumentException>();
        await Assert
            .That(() => identity.Invoke(context, [WasmValue.FromFuncRef(identity)]))
            .ThrowsExactly<ArgumentException>();
    }
}
