namespace WasmSharp.Tests;

internal class WasmHostModule_ConstructorTests
{
    [Test]
    public async Task 引数なしで生成する_空名の空提供元を返す()
    {
        // Act
        var module = new WasmHostModule();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Name).IsEqualTo("");
            await Assert.That(module.Snapshot).IsEmpty();
        }
    }

    [Test]
    [Arguments("")]
    [Arguments(" 環境\0e\u0301 ")]
    public async Task 名前を指定する_正規化せず保持する(string name)
    {
        // Act
        var module = new WasmHostModule(name);

        // Assert
        await Assert.That(module.Name).IsEqualTo(name);
    }

    [Test]
    public async Task 名前がnull_生成時に拒否する()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmHostModule(null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }
}
