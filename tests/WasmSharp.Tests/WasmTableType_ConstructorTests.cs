namespace WasmSharp.Tests;

internal class WasmTableType_ConstructorTests
{
    [Test]
    public async Task 要素型に整数型を指定する_不正な値と参照型が必要なことを示す()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmTableType(WasmValueKind.I32, WasmValueKind.I64, 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("ElementType");
            await Assert.That(exception.ActualValue).IsEqualTo(WasmValueKind.I32);
            await Assert.That(exception.Message).Contains("参照型");
        }
    }

    [Test]
    public async Task アドレス型に浮動小数点型を指定する_不正な値と指定できる整数型を示す()
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmTableType(WasmValueKind.FuncRef, WasmValueKind.F32, 0))
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
