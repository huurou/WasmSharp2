using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_LocalGetTests
{
    [Test]
    public async Task 外側の値の後で引数と追加localsを取得する_宣言順の型とビット列と参照同一性を保って積む()
    {
        // Arrange
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x20), out var descriptor);
        var function = ExecutionFunctionFixture.Create(
            [
                InstructionFixture.Create(0x20, 81, 1),
                InstructionFixture.Create(0x20, 83, 0),
                InstructionFixture.Create(0x20, 85, 2),
                InstructionFixture.Create(0x20, 87, 0),
                InstructionFixture.Create(0x0B, 89),
            ],
            new(
                [WasmValueKind.ExternRef, WasmValueKind.V128],
                [
                    WasmValueKind.V128,
                    WasmValueKind.ExternRef,
                    WasmValueKind.F32,
                    WasmValueKind.ExternRef,
                ]
            ),
            [new(1, WasmValueKind.F32)],
            4
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
                [
                    WasmValue.FromExternRef(reference),
                    WasmValue.FromV128(0x0123456789ABCDEF, 0xFEDCBA9876543210),
                ],
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
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.LocalGet);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.LocalGet);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(4);
            await Assert
                .That(result.Values[0].AsV128())
                .IsEqualTo((0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL));
            await Assert.That(result.Values[1].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(result.Values[2]).IsEqualTo(WasmValue.FromF32Bits(0));
            await Assert.That(result.Values[3].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(actualOuter).IsEqualTo(outer);
        }
    }
}
