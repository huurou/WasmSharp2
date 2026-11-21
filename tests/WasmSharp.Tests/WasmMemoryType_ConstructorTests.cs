namespace WasmSharp.Tests;

internal class WasmMemoryType_ConstructorTests
{
    [Test]
    public async Task アドレス型にF32を指定する_不正な値と指定できる整数型を示して拒否する()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmMemoryType(WasmValueKind.F32, 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("AddressType");
            await Assert.That(exception.ActualValue).IsEqualTo(WasmValueKind.F32);
            await Assert.That(exception.Message).Contains("i32");
            await Assert.That(exception.Message).Contains("i64");
        }
    }
}
