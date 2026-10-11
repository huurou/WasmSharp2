using System.Collections.Immutable;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Execution;

internal class FunctionCode_ConstructorTests
{
    [Test]
    public async Task 既存の3引数で構築する_空の分岐情報と命令位置と圧縮localsと最大operand数を保持する()
    {
        // Arrange
        ImmutableArray<Instruction> instructions =
        [
            new(ExecutionOpcode.Op44, WasmValue.FromF64Bits(0xFFF8123456789ABC), 12345678901, 0),
            new(ExecutionOpcode.Op0B, default, 12345678910, 0),
        ];
        LocalDeclaration[] locals = [new(3, WasmValueKind.I32), new(2, WasmValueKind.ExternRef)];

        // Act
        var code = new FunctionCode(instructions, locals, 7);
        locals[0] = new(100, WasmValueKind.F64);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.Instructions).IsEqualTo(instructions);
            await Assert.That(code.Instructions[0].ByteOffset).IsEqualTo(12345678901L);
            await Assert.That(code.BranchTargets.IsDefault).IsFalse();
            await Assert.That(code.BranchTargets).IsEmpty();
            await Assert.That(code.Locals.Length).IsEqualTo(2);
            await Assert.That(code.Locals[0].Count).IsEqualTo(3u);
            await Assert.That(code.Locals[0].Value).IsEqualTo(WasmValue.FromI32(0));
            await Assert.That(code.Locals[1].Count).IsEqualTo(2u);
            await Assert.That(code.Locals[1].Value).IsEqualTo(WasmValue.FromExternRef(null));
            await Assert.That(code.LocalCount).IsEqualTo(5UL);
            await Assert.That(code.MaxOperandStack).IsEqualTo(7);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task 単一分岐と分岐表を同じ関数へ保持する_開始番号から候補と最後の既定を参照できる(
        int targetCount
    )
    {
        // Arrange
        var singleTarget = new BranchTarget(1, 0, 0);
        var defaultTarget = new BranchTarget(1, 0, 2);
        ImmutableArray<BranchTarget> targets =
            targetCount == 0
                ? [singleTarget, defaultTarget]
                : [singleTarget, new(0, 1, 2), new(1, 3, 2), defaultTarget];
        ImmutableArray<Instruction> instructions =
        [
            new(ExecutionOpcode.Op0B, default, 31, 0),
            new(ExecutionOpcode.Op0B, default, 32, 1, targetCount),
        ];

        // Act
        var code = new FunctionCode(instructions, [], 6, targets);
        var single = code.BranchTargets[(int)code.Instructions[0].Index];
        var tableStart = (int)code.Instructions[1].Index;
        var tableDefault = code.BranchTargets[tableStart + code.Instructions[1].TargetCount];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.BranchTargets.SequenceEqual(targets)).IsTrue();
            await Assert.That(code.BranchTargets.Length).IsEqualTo(targetCount + 2);
            await Assert.That(single).IsEqualTo(singleTarget);
            await Assert.That(tableDefault).IsEqualTo(defaultTarget);
            await Assert.That(tableDefault.TargetPc).IsEqualTo(1);
            await Assert.That(tableDefault.StackHeight).IsEqualTo(0);
            await Assert.That(tableDefault.KeepCount).IsEqualTo(2);
            await Assert.That(code.Instructions[1].ByteOffset).IsEqualTo(32L);
            await Assert.That(code.MaxOperandStack).IsEqualTo(6);
        }
    }
}
