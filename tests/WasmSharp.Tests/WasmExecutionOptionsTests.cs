namespace WasmSharp.Tests;

internal class WasmExecutionOptions_ConstructorTests
{
    [Test]
    [Arguments(1)]
    [Arguments(100)]
    [Arguments(int.MaxValue)]
    public async Task 正の上限を指定する_指定した深さを保持する(int limit)
    {
        // Act
        var options = new WasmExecutionOptions(limit);

        // Assert
        await Assert.That(options.MaxCallDepth).IsEqualTo(limit);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task 正でない上限を指定する_引数不正として拒否する(int limit)
    {
        // Act & Assert
        await Assert
            .That(() => new WasmExecutionOptions(limit))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}

internal class WasmExecutionOptions_DefaultTests
{
    [Test]
    public async Task 既定のポリシーを取得する_最大呼び出し深さが1024になる()
    {
        // Act
        var options = WasmExecutionOptions.Default;

        // Assert
        await Assert.That(options.MaxCallDepth).IsEqualTo(1024);
    }
}
