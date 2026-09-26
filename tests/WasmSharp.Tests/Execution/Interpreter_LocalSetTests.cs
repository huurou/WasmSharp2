using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_LocalSetTests
{
    [Test]
    public async Task 外側の値の後で引数と追加localsへ設定する_最上位の値を除き後続の取得で設定値を返し外側を変えない()
    {
        // Arrange
        var found = InstructionSet.TryGet(new(0, 0x21), out var descriptor);
        var function = ExecutionFunctionFixture.Create(
            [
                InstructionFixture.Create(0x42, 81, immediate: WasmValue.FromI64(5)),
                InstructionFixture.Create(0x42, 83, immediate: WasmValue.FromI64(9)),
                InstructionFixture.Create(0x21, 85, 0),
                InstructionFixture.Create(0x20, 87, 0),
                InstructionFixture.Create(
                    0x44,
                    89,
                    immediate: WasmValue.FromF64Bits(0xFFF4000000000001)
                ),
                InstructionFixture.Create(0x21, 98, 1),
                InstructionFixture.Create(0x20, 100, 1),
                InstructionFixture.Create(0x0B, 102),
            ],
            new([WasmValueKind.I64], [WasmValueKind.I64, WasmValueKind.I64, WasmValueKind.F64]),
            [new(1, WasmValueKind.F64)],
            3
        );
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var outer = WasmValue.FromI64(99);
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        WasmValue actualOuter;

        // Act
        try
        {
            context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(frame);
            context.PushValue(outer);
            result = Interpreter.Run(
                context,
                function,
                [WasmValue.FromI64(1)],
                WasmProcessingStage.Invoke
            );
            actualOuter = context.GetValue(0);
        }
        finally
        {
            context.Restore(0, 0, 0);
            InterpreterContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(found).IsTrue();
            await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.Index);
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.LocalSet);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.LocalSet);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(3);
            await Assert.That(result.Values[0].AsI64()).IsEqualTo(5L);
            await Assert.That(result.Values[1].AsI64()).IsEqualTo(9L);
            await Assert.That(result.Values[2].AsF64Bits()).IsEqualTo(0xFFF4000000000001UL);
            await Assert.That(actualOuter).IsEqualTo(outer);
        }
    }
}
