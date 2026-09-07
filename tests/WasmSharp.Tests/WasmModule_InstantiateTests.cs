using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_InstantiateTests
{
    [Test]
    public async Task 検証済みの定義を繰り返しインスタンス化する_別の実体に指定と既定のポリシーを保持する()
    {
        // Arrange
        var module = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate();
        var options = new WasmExecutionOptions(7);

        // Act
        var first = module.Instantiate([], options);
        var second = module.Instantiate([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(ReferenceEquals(first.ExecutionOptions, options)).IsTrue();
            await Assert
                .That(ReferenceEquals(second.ExecutionOptions, WasmExecutionOptions.Default))
                .IsTrue();
        }
    }

    [Test]
    public async Task 検証していない_状態の契約違反として拒否する()
    {
        // Arrange
        var module = WasmModule.Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B));

        // Act & Assert
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }
}
