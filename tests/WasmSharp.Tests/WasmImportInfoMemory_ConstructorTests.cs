namespace WasmSharp.Tests;

internal class WasmImportInfoMemory_ConstructorTests
{
    [Test]
    public async Task ページ数の上下限がnullである_Limitsを指す引数例外を返す()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmImportInfo.Memory("env", "m", null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("Limits");
    }
}
