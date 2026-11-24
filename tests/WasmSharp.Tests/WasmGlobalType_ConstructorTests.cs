namespace WasmSharp.Tests;

internal class WasmGlobalType_ConstructorTests
{
    [Test]
    [Arguments(WasmValueKind.F64, true)]
    [Arguments(WasmValueKind.ExternRef, false)]
    public async Task Globalの型を記述する_値型と可変性を保持して別の記述へコピーできる(
        WasmValueKind valueKind,
        bool isMutable
    )
    {
        // Arrange
        var type = new WasmGlobalType(valueKind, isMutable);

        // Act
        var changed = type with { ValueKind = WasmValueKind.I32, IsMutable = !isMutable };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(type.ValueKind).IsEqualTo(valueKind);
            await Assert.That(type.IsMutable).IsEqualTo(isMutable);
            await Assert.That(changed.ValueKind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(changed.IsMutable).IsEqualTo(!isMutable);
        }
    }
}

internal class WasmGlobalType_ToStringTests
{
    [Test]
    public async Task 非nullの参照を保持する変更可能なglobalを表示する_宣言した型と可変性が分かる()
    {
        // Arrange
        var type = new WasmGlobalType(WasmValueType.Reference(WasmValueKind.ExternRef, false), true);

        // Act
        var text = type.ToString();

        // Assert
        await Assert.That(text).IsEqualTo("WasmGlobalType { ValueKind = (ref extern), IsMutable = True }");
    }
}
