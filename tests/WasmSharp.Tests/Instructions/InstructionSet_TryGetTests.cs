using System.Reflection;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Instructions;

internal class InstructionSet_TryGetTests
{
    [Test]
    [Arguments((byte)0, 183)]
    [Arguments((byte)0xFC, 18)]
    [Arguments((byte)0xFD, 236)]
    public async Task 保存版の命令割当を照合する_番号と名前と欠番が一致し対応済み命令だけを実行対象にする(
        byte prefix,
        int expectedCount
    )
    {
        // Arrange
        var expected = Core2InstructionFixture.Read(prefix);
        var actual = new Dictionary<uint, string>();
        var executable = new List<uint>();
        var inconsistent = new List<uint>();

        // Act
        for (uint code = 0; code <= 255; code++)
        {
            if (!InstructionSet.TryGet(new(prefix, code), out var descriptor))
            {
                continue;
            }
            actual.Add(code, descriptor.Name);
            if (descriptor.ExecutionOpcode is not null)
            {
                executable.Add(code);
            }
            else if (
                descriptor.Immediate != ImmediateKind.Unsupported
                || descriptor.StackEffect != StackEffectKind.Unsupported
                || descriptor.Validation != ValidationRule.Unsupported
            )
            {
                inconsistent.Add(code);
            }
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(expected.Count).IsEqualTo(expectedCount);
            await Assert.That(actual).IsEquivalentTo(expected);
            await Assert.That(inconsistent).IsEmpty();
            await Assert
                .That(executable)
                .IsEquivalentTo(
                    prefix == 0
                        ? new uint[]
                        {
                            0x00,
                            0x01,
                            0x02,
                            0x03,
                            0x04,
                            0x05,
                            0x0B,
                            0x0C,
                            0x0D,
                            0x0E,
                            0x0F,
                            0x10,
                            0x11,
                            0x1A,
                            0x1B,
                            0x1C,
                            0x20,
                            0x21,
                            0x22,
                            0x23,
                            0x24,
                            0x25,
                            0x26,
                            0x3F,
                            0x40,
                            0x41,
                            0x42,
                            0x43,
                            0x44,
                            0xD0,
                            0xD1,
                            0xD2,
                        }
                            .Concat(Enumerable.Range(0x28, 23).Select(x => (uint)x))
                            .Concat(Enumerable.Range(0x45, 128).Select(x => (uint)x))
                    : prefix == 0xFC ? Enumerable.Range(0, 18).Select(x => (uint)x)
                    : []
                );
            await Assert.That(InstructionSet.TryGet(new(prefix, 256), out _)).IsFalse();
            await Assert.That(InstructionSet.TryGet(new(prefix, uint.MaxValue), out _)).IsFalse();
        }
    }

    [Test]
    [Arguments(0x01, ImmediateKind.None, StackEffectKind.None, ValidationRule.None, "NoOp")]
    [Arguments(0x02, ImmediateKind.BlockType, StackEffectKind.Block, ValidationRule.Block, "NoOp")]
    [Arguments(0x03, ImmediateKind.BlockType, StackEffectKind.Loop, ValidationRule.Loop, "NoOp")]
    [Arguments(
        0x04,
        ImmediateKind.BlockType,
        StackEffectKind.If,
        ValidationRule.If,
        "BranchIfZero"
    )]
    [Arguments(0x05, ImmediateKind.None, StackEffectKind.Else, ValidationRule.Else, "Branch")]
    [Arguments(
        0x0B,
        ImmediateKind.None,
        StackEffectKind.FunctionEnd,
        ValidationRule.FunctionEnd,
        "Return"
    )]
    [Arguments(0x0C, ImmediateKind.Index, StackEffectKind.Branch, ValidationRule.Branch, "Branch")]
    [Arguments(
        0x0D,
        ImmediateKind.Index,
        StackEffectKind.BranchIf,
        ValidationRule.BranchIf,
        "BranchIf"
    )]
    [Arguments(
        0x0E,
        ImmediateKind.BranchTable,
        StackEffectKind.BranchTable,
        ValidationRule.BranchTable,
        "BranchTable"
    )]
    [Arguments(0x1B, ImmediateKind.None, StackEffectKind.Select, ValidationRule.Select, "Select")]
    [Arguments(
        0xD0,
        ImmediateKind.ReferenceType,
        StackEffectKind.PushReference,
        ValidationRule.Constant,
        "PushConstant"
    )]
    [Arguments(
        0xD1,
        ImmediateKind.None,
        StackEffectKind.RefIsNull,
        ValidationRule.RefIsNull,
        "RefIsNull"
    )]
    [Arguments(
        0xD2,
        ImmediateKind.Index,
        StackEffectKind.RefFunc,
        ValidationRule.RefFunc,
        "RefFunc"
    )]
    [Arguments(
        0x1C,
        ImmediateKind.TypeVector,
        StackEffectKind.TypedSelect,
        ValidationRule.TypedSelect,
        "Select"
    )]
    public async Task 制御とselectと参照の宣言を確認する_即値と型効果と規則と共有handlerが一致する(
        uint code,
        ImmediateKind immediate,
        StackEffectKind stackEffect,
        ValidationRule validation,
        string handler
    )
    {
        // Arrange
        var names = Core2InstructionFixture.Read(0);
        var declaration = typeof(InstructionSet)
            .GetCustomAttributes<InstructionAttribute>()
            .Single(x => x.Prefix == 0 && x.Code == code);

        // Act
        var found = InstructionSet.TryGet(new(0, code), out var descriptor);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(found).IsTrue();
            await Assert.That(descriptor.Name).IsEqualTo(names[code]);
            await Assert.That(descriptor.Immediate).IsEqualTo(immediate);
            await Assert.That(descriptor.StackEffect).IsEqualTo(stackEffect);
            await Assert.That(descriptor.Validation).IsEqualTo(validation);
            await Assert.That(descriptor.ExecutionOpcode).IsNotNull();
            await Assert.That(descriptor.NumericSignature).IsNull();
            await Assert.That(declaration.ExecutionHandler).IsEqualTo(handler);
        }
    }

    [Test]
    [Arguments((byte)0, 128)]
    [Arguments((byte)0xFC, 8)]
    public async Task 全数値命令の宣言を固定仕様と照合する_入力個数と型と結果型とhandlerが一致する(
        byte prefix,
        int count
    )
    {
        // Arrange
        var names = Core2InstructionFixture.Read(prefix);
        var declarations = typeof(InstructionSet)
            .GetCustomAttributes<InstructionAttribute>()
            .ToDictionary(x => new OpcodeKey(x.Prefix, x.Code));
        var codes = prefix == 0 ? Enumerable.Range(0x45, 128) : Enumerable.Range(0, 8);

        // Act & Assert
        await Assert.That(codes.Count()).IsEqualTo(count);
        foreach (var code in codes)
        {
            var key = new OpcodeKey(prefix, (uint)code);
            var found = InstructionSet.TryGet(key, out var descriptor);
            var name = names[(uint)code];
            var handler = string.Concat(
                name.Split('.', '_').Select(x => char.ToUpperInvariant(x[0]) + x[1..])
            );
            var signature =
                prefix == 0
                    ? ExpectedNumericSignature((uint)code)
                    : new NumericSignature(
                        1,
                        code % 4 < 2 ? WasmValueKind.F32 : WasmValueKind.F64,
                        code < 4 ? WasmValueKind.I32 : WasmValueKind.I64
                    );
            using (Assert.Multiple())
            {
                await Assert.That(found).IsTrue();
                await Assert.That(descriptor.Name).IsEqualTo(name);
                await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.None);
                await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.Numeric);
                await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.Numeric);
                await Assert
                    .That(descriptor.NumericSignature)
                    .IsEqualTo((NumericSignature?)signature);
                await Assert.That(descriptor.ExecutionOpcode).IsNotNull();
                await Assert.That(declarations[key].ExecutionHandler).IsEqualTo(handler);
            }
        }
    }

    [Test]
    public async Task 定数4命令の宣言を確認する_既存の定数規則を保ちNumericSignatureを持たない()
    {
        // Arrange
        uint[] codes = [0x41, 0x42, 0x43, 0x44];

        // Act & Assert
        foreach (var code in codes)
        {
            InstructionSet.TryGet(new(0, code), out var descriptor);
            using (Assert.Multiple())
            {
                await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.Constant);
                await Assert.That(descriptor.NumericSignature).IsNull();
            }
        }
    }

    // 固定Core2の型規則とopcode割当から求める期待値 製品の宣言は参照しない
    private static NumericSignature ExpectedNumericSignature(uint code)
    {
        return code switch
        {
            0x45 => new(1, WasmValueKind.I32, WasmValueKind.I32),
            >= 0x46 and <= 0x4F => new(2, WasmValueKind.I32, WasmValueKind.I32),
            0x50 => new(1, WasmValueKind.I64, WasmValueKind.I32),
            >= 0x51 and <= 0x5A => new(2, WasmValueKind.I64, WasmValueKind.I32),
            >= 0x5B and <= 0x60 => new(2, WasmValueKind.F32, WasmValueKind.I32),
            >= 0x61 and <= 0x66 => new(2, WasmValueKind.F64, WasmValueKind.I32),
            >= 0x67 and <= 0x69 => new(1, WasmValueKind.I32, WasmValueKind.I32),
            >= 0x6A and <= 0x78 => new(2, WasmValueKind.I32, WasmValueKind.I32),
            >= 0x79 and <= 0x7B => new(1, WasmValueKind.I64, WasmValueKind.I64),
            >= 0x7C and <= 0x8A => new(2, WasmValueKind.I64, WasmValueKind.I64),
            >= 0x8B and <= 0x91 => new(1, WasmValueKind.F32, WasmValueKind.F32),
            >= 0x92 and <= 0x98 => new(2, WasmValueKind.F32, WasmValueKind.F32),
            >= 0x99 and <= 0x9F => new(1, WasmValueKind.F64, WasmValueKind.F64),
            >= 0xA0 and <= 0xA6 => new(2, WasmValueKind.F64, WasmValueKind.F64),
            0xA7 => new(1, WasmValueKind.I64, WasmValueKind.I32),
            0xA8 or 0xA9 => new(1, WasmValueKind.F32, WasmValueKind.I32),
            0xAA or 0xAB => new(1, WasmValueKind.F64, WasmValueKind.I32),
            0xAC or 0xAD => new(1, WasmValueKind.I32, WasmValueKind.I64),
            0xAE or 0xAF => new(1, WasmValueKind.F32, WasmValueKind.I64),
            0xB0 or 0xB1 => new(1, WasmValueKind.F64, WasmValueKind.I64),
            0xB2 or 0xB3 => new(1, WasmValueKind.I32, WasmValueKind.F32),
            0xB4 or 0xB5 => new(1, WasmValueKind.I64, WasmValueKind.F32),
            0xB6 => new(1, WasmValueKind.F64, WasmValueKind.F32),
            0xB7 or 0xB8 => new(1, WasmValueKind.I32, WasmValueKind.F64),
            0xB9 or 0xBA => new(1, WasmValueKind.I64, WasmValueKind.F64),
            0xBB => new(1, WasmValueKind.F32, WasmValueKind.F64),
            0xBC => new(1, WasmValueKind.F32, WasmValueKind.I32),
            0xBD => new(1, WasmValueKind.F64, WasmValueKind.I64),
            0xBE => new(1, WasmValueKind.I32, WasmValueKind.F32),
            0xBF => new(1, WasmValueKind.I64, WasmValueKind.F64),
            0xC0 or 0xC1 => new(1, WasmValueKind.I32, WasmValueKind.I32),
            >= 0xC2 and <= 0xC4 => new(1, WasmValueKind.I64, WasmValueKind.I64),
            _ => throw new ArgumentOutOfRangeException(nameof(code)),
        };
    }
}
