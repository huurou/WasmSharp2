using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class BlockType_EmptyTests
{
    [Test]
    public async Task 空型を指定する_i32や型添字0とは異なるタグになる()
    {
        // Arrange
        var value = BlockType.FromValue(WasmValueKind.I32);
        var indexed = BlockType.FromTypeIndex(0);

        // Act
        var empty = BlockType.Empty;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(empty.Kind).IsEqualTo(BlockTypeKind.Empty);
            await Assert.That(value.Kind).IsEqualTo(BlockTypeKind.Value);
            await Assert.That(indexed.Kind).IsEqualTo(BlockTypeKind.TypeIndex);
            await Assert.That(empty.ValueType).IsEqualTo(default(WasmValueKind));
            await Assert.That(empty.TypeIndex).IsEqualTo(0u);
        }
    }
}

internal class BlockType_FromValueTests
{
    [Test]
    [Arguments(WasmValueKind.I32)]
    [Arguments(WasmValueKind.I64)]
    [Arguments(WasmValueKind.F32)]
    [Arguments(WasmValueKind.F64)]
    [Arguments(WasmValueKind.V128)]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 単一の値型を指定する_値型タグと指定型を保持する(WasmValueKind valueType)
    {
        // Arrange
        var expected = valueType;

        // Act
        var blockType = BlockType.FromValue(valueType);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(blockType.Kind).IsEqualTo(BlockTypeKind.Value);
            await Assert.That(blockType.ValueType).IsEqualTo(expected);
            await Assert.That(blockType.TypeIndex).IsEqualTo(0u);
        }
    }
}

internal class BlockType_FromTypeIndexTests
{
    [Test]
    [Arguments(0u)]
    [Arguments(uint.MaxValue)]
    public async Task 非負の型添字を指定する_存在検査をせず添字タグと値を保持する(uint typeIndex)
    {
        // Arrange
        var expected = typeIndex;

        // Act
        var blockType = BlockType.FromTypeIndex(typeIndex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(blockType.Kind).IsEqualTo(BlockTypeKind.TypeIndex);
            await Assert.That(blockType.TypeIndex).IsEqualTo(expected);
            await Assert.That(blockType.ValueType).IsEqualTo(default(WasmValueKind));
        }
    }
}
