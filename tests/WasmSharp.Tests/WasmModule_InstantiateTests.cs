using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Contextがnullの生成_提供登録の両形式で引数名付きで拒否する(bool useImports)
    {
        // Arrange
        var module = WasmModule.Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B)).Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => useImports ? module.Instantiate(null!, new WasmImports()) : module.Instantiate(null!, []))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("context");
    }

    [Test]
    public async Task 検証済みの定義を繰り返しインスタンス化する_別の実体に指定と既定のポリシーを保持する()
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var module = WasmModule.Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B)).Validate();
        var options = new WasmExecutionOptions(7);

        // Act
        var first = module.Instantiate(executionContext, [], options);
        var second = module.Instantiate(executionContext, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(ReferenceEquals(first.ExecutionOptions, options)).IsTrue();
            await Assert.That(ReferenceEquals(second.ExecutionOptions, WasmExecutionOptions.Default)).IsTrue();
        }
    }

    [Test]
    [Arguments("empty")]
    [Arguments("null")]
    [Arguments("duplicate")]
    public async Task 検証していない_提供登録の検査より先に状態の契約違反として拒否する(string providerState)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var module = WasmModule.Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B));
        var provider = new WasmHostModule("env");
        provider.Define(
            "f",
            WasmFunction.CreateHost(new WasmFunctionType([], []), (callbackContext, _) => new WasmResults([]))
        );
        WasmHostModule[] hostModules = providerState switch
        {
            "empty" => [],
            "null" => [null!],
            _ => [provider, provider],
        };

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate(executionContext, hostModules))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("検証の成功");
    }
}
