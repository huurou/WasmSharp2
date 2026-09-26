using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_DropTests
{
    [Test]
    public async Task 最上位の値を破棄する_残る値の順序と参照同一性を変えない()
    {
        // Arrange
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x1A), out var descriptor);
        var function = ExecutionFunctionFixture.Create(
            [
                InstructionFixture.Create(0x41, 81, immediate: WasmValue.FromI32(7)),
                InstructionFixture.Create(0x20, 83, 0),
                InstructionFixture.Create(0x42, 85, immediate: WasmValue.FromI64(3)),
                InstructionFixture.Create(0x1A, 87),
                InstructionFixture.Create(0x0B, 88),
            ],
            new([WasmValueKind.ExternRef], [WasmValueKind.I32, WasmValueKind.ExternRef]),
            [],
            3
        );
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;

        // Act
        try
        {
            result = Interpreter.Run(
                context,
                function,
                [WasmValue.FromExternRef(reference)],
                WasmProcessingStage.Invoke
            );
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
            await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.None);
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.Drop);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.Drop);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values.Length).IsEqualTo(2);
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(7);
            await Assert.That(result.Values[1].AsExternRef()).IsSameReferenceAs(reference);
        }
    }
}
