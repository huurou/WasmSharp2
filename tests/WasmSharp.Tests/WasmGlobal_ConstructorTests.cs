namespace WasmSharp.Tests;

internal class WasmGlobal_ConstructorTests
{
    [Test]
    public async Task 型と初期値を指定する_型とビット列を保持する()
    {
        // Arrange
        var type = new WasmGlobalType(WasmValueKind.F64, false);
        var initialValue = WasmValue.FromF64Bits(0xFFF8000000000042);

        // Act
        var global = new WasmGlobal(type, initialValue);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(global.Type).IsEqualTo(type);
            await Assert.That(global.Value.AsF64Bits()).IsEqualTo(0xFFF8000000000042UL);
        }
    }

    [Test]
    public async Task 型が初期値と一致しない_初期値の引数名を示して拒否する()
    {
        // Arrange
        var type = new WasmGlobalType(WasmValueKind.I64, true);

        // Act & Assert
        var exception = await Assert
            .That(() => new WasmGlobal(type, WasmValue.FromI32(1)))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("initialValue");
    }

    [Test]
    [Arguments(WasmValueKind.ExnRef)]
    [Arguments((WasmValueKind)255)]
    public async Task 型が未対応である_型の引数名を示して拒否する(WasmValueKind kind)
    {
        // Arrange
        var type = new WasmGlobalType(kind, true);

        // Act & Assert
        var exception = await Assert
            .That(() => new WasmGlobal(type, WasmValue.FromI32(1)))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("type");
    }

    [Test]
    public async Task 型がnullである_引数例外として拒否する()
    {
        // Act & Assert
        await Assert
            .That(() => new WasmGlobal(null!, default))
            .ThrowsExactly<ArgumentNullException>();
    }
}
