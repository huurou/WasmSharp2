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

internal class WasmTableType_ToStringTests
{
    [Test]
    public async Task 参照型を指定した64ビットテーブルを表示する_宣言した型と要素数の上下限が分かる()
    {
        // Arrange
        var type = new WasmTableType(WasmValueType.Reference(WasmValueKind.FuncRef, false), WasmValueKind.I64, 2, 9);

        // Act
        var text = type.ToString();

        // Assert
        await Assert
            .That(text)
            .IsEqualTo(
                "WasmTableType { MinimumElements = 2, MaximumElements = 9, ElementType = (ref func), AddressType = I64 }"
            );
    }
}
