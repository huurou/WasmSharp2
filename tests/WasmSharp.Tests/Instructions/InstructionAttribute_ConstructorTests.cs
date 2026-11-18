using WasmSharp.Instructions;

namespace WasmSharp.Tests.Instructions;

internal class InstructionAttribute_ConstructorTests
{
    [Test]
    public async Task 異なる入力型を持つ命令を宣言する_型の順序を保持し元配列から独立する()
    {
        // Arrange
        WasmValueKind[] inputs = [WasmValueKind.V128, WasmValueKind.I32];

        // Act
        var declaration = new InstructionAttribute(
            0xFD,
            0x6B,
            "i8x16.shl",
            ImmediateKind.None,
            StackEffectKind.Numeric,
            ValidationRule.Numeric,
            "I8x16Shl",
            inputs,
            WasmValueKind.V128
        );
        inputs[0] = WasmValueKind.F64;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(declaration.NumericSignature).IsNotNull();
            await Assert
                .That(declaration.NumericSignature!.Value.InputTypes.SequenceEqual([WasmValueKind.V128, WasmValueKind.I32]))
                .IsTrue();
            await Assert.That(declaration.NumericSignature.Value.ResultType).IsEqualTo(WasmValueKind.V128);
        }
    }

    [Test]
    public async Task 同じ型の入力数を指定する_個数と結果型を保持する()
    {
        // Arrange & Act
        var declaration = new InstructionAttribute(
            0,
            0x7C,
            "i64.add",
            ImmediateKind.None,
            StackEffectKind.Numeric,
            ValidationRule.Numeric,
            "I64Add",
            2,
            WasmValueKind.I64,
            WasmValueKind.I64
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(declaration.NumericSignature).IsNotNull();
            await Assert
                .That(declaration.NumericSignature!.Value.InputTypes.SequenceEqual([WasmValueKind.I64, WasmValueKind.I64]))
                .IsTrue();
            await Assert.That(declaration.NumericSignature.Value.ResultType).IsEqualTo(WasmValueKind.I64);
        }
    }

    [Test]
    public async Task 狭い整数ロードを宣言する_値型とアクセス幅と符号拡張を保持する()
    {
        // Arrange & Act
        var declaration = new InstructionAttribute(
            0,
            0x30,
            "i64.load8_s",
            ImmediateKind.MemoryArgument,
            StackEffectKind.MemoryLoad,
            ValidationRule.MemoryLoad,
            "Load",
            WasmValueKind.I64,
            1,
            true
        );

        // Assert
        await Assert
            .That(declaration.MemoryAccessSignature)
            .IsEqualTo(new MemoryAccessSignature(WasmValueKind.I64, 1, true));
    }
}
