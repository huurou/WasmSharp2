using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_LocalTeeTests
{
    [Test]
    public async Task 最上位の値をlocalへ設定する_同じ参照をoperandに残し後続の取得でも返す()
    {
        // Arrange
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x22), out var descriptor);
        var function = ExecutionFunctionFixture.Create(
            [
                InstructionFixture.Create(0x41, 81, immediate: WasmValue.FromI32(7)),
                InstructionFixture.Create(0x20, 83, 0),
                InstructionFixture.Create(0x22, 85, 1),
                InstructionFixture.Create(0x20, 87, 1),
                InstructionFixture.Create(0x0B, 89),
            ],
            new(
                [WasmValueKind.ExternRef, WasmValueKind.ExternRef],
                [WasmValueKind.I32, WasmValueKind.ExternRef, WasmValueKind.ExternRef]
            ),
            [],
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
                [WasmValue.FromExternRef(reference), WasmValue.FromExternRef(new object())],
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
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.LocalTee);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.LocalTee);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(3);
            await Assert.That(result.Values[0]).IsEqualTo(WasmValue.FromI32(7));
            await Assert.That(result.Values[1].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(result.Values[2].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(actualOuter).IsEqualTo(outer);
        }
    }
}
