using WasmSharp.Instructions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class DecodedInstruction_WithoutImmediateTests
{
    [Test]
    public async Task 即値を持たない命令_不要項目を既定値と非defaultの空配列に統一する()
    {
        // Arrange
        var opcode = new OpcodeKey(0, 0x0B);

        // Act
        var instruction = DecodedInstruction.WithoutImmediate(opcode, 12345678901L);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Opcode).IsEqualTo(opcode);
            await Assert.That(instruction.ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(instruction.Immediate).IsEqualTo(default(WasmValue));
            await Assert.That(instruction.Index).IsEqualTo(0u);
            await Assert.That(instruction.BlockType).IsEqualTo(default(BlockType));
            await Assert.That(instruction.Labels.IsDefault).IsFalse();
            await Assert.That(instruction.Labels).IsEmpty();
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.SelectTypes).IsEmpty();
        }
    }
}

internal class DecodedInstruction_WithImmediateTests
{
    [Test]
    public async Task 定数の即値_ビット列を保持し他の即値を残さない()
    {
        // Arrange
        var value = WasmValue.FromF32Bits(0xFFC12345);

        // Act
        var instruction = DecodedInstruction.WithImmediate(new(0, 0x43), value, 50);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Immediate.AsF32Bits()).IsEqualTo(0xFFC12345u);
            await Assert.That(instruction.Index).IsEqualTo(0u);
            await Assert.That(instruction.BlockType).IsEqualTo(default(BlockType));
            await Assert.That(instruction.Labels.IsDefault).IsFalse();
            await Assert.That(instruction.Labels).IsEmpty();
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.SelectTypes).IsEmpty();
        }
    }
}

internal class DecodedInstruction_WithIndexTests
{
    [Test]
    public async Task 最大の添字_値即値と混同せず保持する()
    {
        // Arrange
        var opcode = new OpcodeKey(0, 0x10);

        // Act
        var instruction = DecodedInstruction.WithIndex(opcode, uint.MaxValue, 50);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Index).IsEqualTo(uint.MaxValue);
            await Assert.That(instruction.Immediate).IsEqualTo(default(WasmValue));
            await Assert.That(instruction.BlockType).IsEqualTo(default(BlockType));
            await Assert.That(instruction.Labels.IsDefault).IsFalse();
            await Assert.That(instruction.Labels).IsEmpty();
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.SelectTypes).IsEmpty();
        }
    }
}

internal class DecodedInstruction_WithBlockTypeTests
{
    [Test]
    public async Task 型添字の指定_タグ付き型を保持し他の即値を残さない()
    {
        // Arrange
        var blockType = BlockType.FromTypeIndex(uint.MaxValue);

        // Act
        var instruction = DecodedInstruction.WithBlockType(new(0, 0x02), blockType, 50);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.BlockType.Kind).IsEqualTo(BlockTypeKind.TypeIndex);
            await Assert.That(instruction.BlockType.TypeIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(instruction.Immediate).IsEqualTo(default(WasmValue));
            await Assert.That(instruction.Index).IsEqualTo(0u);
            await Assert.That(instruction.Labels.IsDefault).IsFalse();
            await Assert.That(instruction.Labels).IsEmpty();
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.SelectTypes).IsEmpty();
        }
    }
}

internal class DecodedInstruction_WithBranchTableTests
{
    [Test]
    public async Task 候補列の元配列を変更する_宣言順と末尾の既定ラベルを不変に所有する()
    {
        // Arrange
        uint[] labels = [2, 0, uint.MaxValue];

        // Act
        var instruction = DecodedInstruction.WithBranchTable(new(0, 0x0E), labels, 5, 50);
        labels[0] = 99;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(instruction.Labels.SequenceEqual([2u, 0u, uint.MaxValue, 5u]))
                .IsTrue();
            await Assert.That(instruction.Labels.IsDefault).IsFalse();
            await Assert.That(instruction.Immediate).IsEqualTo(default(WasmValue));
            await Assert.That(instruction.Index).IsEqualTo(0u);
            await Assert.That(instruction.BlockType).IsEqualTo(default(BlockType));
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.SelectTypes).IsEmpty();
        }
    }

    [Test]
    public async Task 候補が空_既定ラベルだけの非空列を保持する()
    {
        // Arrange
        var opcode = new OpcodeKey(0, 0x0E);

        // Act
        var instruction = DecodedInstruction.WithBranchTable(opcode, [], uint.MaxValue, 50);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.Labels.Length).IsEqualTo(1);
            await Assert.That(instruction.Labels[0]).IsEqualTo(uint.MaxValue);
        }
    }
}

internal class DecodedInstruction_WithSelectTypesTests
{
    [Test]
    public async Task 型列の元配列を変更する_全値型と宣言順を不変に所有する()
    {
        // Arrange
        WasmValueKind[] types =
        [
            WasmValueKind.I32,
            WasmValueKind.I64,
            WasmValueKind.F32,
            WasmValueKind.F64,
            WasmValueKind.V128,
            WasmValueKind.FuncRef,
            WasmValueKind.ExternRef,
        ];
        var expected = (WasmValueKind[])types.Clone();

        // Act
        var instruction = DecodedInstruction.WithSelectTypes(new(0, 0x1C), types, 50);
        types[0] = WasmValueKind.F64;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.SelectTypes.SequenceEqual(expected)).IsTrue();
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.Immediate).IsEqualTo(default(WasmValue));
            await Assert.That(instruction.Index).IsEqualTo(0u);
            await Assert.That(instruction.BlockType).IsEqualTo(default(BlockType));
            await Assert.That(instruction.Labels.IsDefault).IsFalse();
            await Assert.That(instruction.Labels).IsEmpty();
        }
    }

    [Test]
    public async Task 型列が空_長さの型検証をせず非defaultの空配列を保持する()
    {
        // Arrange
        var opcode = new OpcodeKey(0, 0x1C);

        // Act
        var instruction = DecodedInstruction.WithSelectTypes(opcode, [], 50);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instruction.SelectTypes.IsDefault).IsFalse();
            await Assert.That(instruction.SelectTypes).IsEmpty();
        }
    }
}
