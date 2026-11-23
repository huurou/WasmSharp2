namespace WasmSharp.Tests;

internal class WasmImportInfoTable_ConstructorTests
{
    [Test]
    public async Task 要素数の上下限にnullを渡す_Limitsを指す引数例外を返す()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmImportInfo.Table("env", "t", WasmValueKind.FuncRef, null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("Limits");
    }
}
