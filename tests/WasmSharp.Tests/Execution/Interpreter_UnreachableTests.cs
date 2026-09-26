using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_UnreachableTests
{
    [Test]
    public async Task 後続命令がある_元の関数indexと命令位置のtrapを返し後続を実行せず外側を復元する()
    {
        // Arrange
        var found = InstructionSet.TryGet(new(0, 0x00), out var descriptor);
        var function = ExecutionFunctionFixture.Create([
            InstructionFixture.Create(0x41, 81, immediate: WasmValue.FromI32(1)),
            InstructionFixture.Create(0x00, 12345678903),
            new((ExecutionOpcode)int.MaxValue, default, 12345678904, 0),
            InstructionFixture.Create(0x0B, 12345678905),
        ]);
        var frame = new ExecutionFrame(FunctionFixture.Create(), 0, 0) { Pc = 17 };
        var value = WasmValue.FromExternRef(new object());
        var context = InterpreterContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        ExecutionFrame actualFrame;
        WasmValue actualValue;
        WasmValue cleared;
        (int Frames, int Values, int Depth) state;

        // Act
        try
        {
            context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(frame);
            context.PushValue(value);
            result = Interpreter.Run(context, function, [], WasmProcessingStage.Invoke);
            actualFrame = context.GetFrame(0);
            actualValue = context.GetValue(0);
            cleared = context.GetValue(1);
            state = (context.FrameCount, context.ValueCount, context.CallDepth);
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
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.Unreachable);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.Unreachable);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Trap);
            await Assert.That(result.TrapReason).IsEqualTo(WasmTrapReason.Unreachable);
            await Assert.That(result.FunctionIndex).IsEqualTo(1U);
            await Assert.That(result.ByteOffset).IsEqualTo(12345678903);
            await Assert.That(result.Values).IsEmpty();
            await Assert.That(actualFrame).IsEqualTo(frame);
            await Assert.That(actualValue).IsEqualTo(value);
            await Assert.That(cleared).IsEqualTo(default);
            await Assert.That(state).IsEqualTo((1, 1, 1));
        }
    }
}
