using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

public class Interpreter_ReturnTests
{
    [Test]
    public async Task 内側の関数を終了する_結果を引数開始位置へ保持し外側の値とフレームを残す()
    {
        // Arrange
        var outerFunction = FunctionFixture.Create();
        var module = new WasmModule(
            [new([WasmValueKind.I32], [WasmValueKind.ExternRef, WasmValueKind.I64])],
            [new(0, 80, [new(1, WasmValueKind.I32)], [])],
            [],
            90
        );
        var function = new WasmInstance(module, WasmExecutionOptions.Default).Functions[0];
        var reference = new object();
        var found = InstructionSet.TryGet(new(0, 0x0B), out var descriptor);
        var instruction = new Instruction(descriptor.ExecutionOpcode!.Value, default, 88);
        var context = WasmExecutionContext.Enter(new(10), out var isOutermost);
        ExecutionResult result;
        ExecutionFrame outerFrame;
        ExecutionFrame removedFrame;
        WasmValue outerValue;
        WasmValue first;
        WasmValue second;
        WasmValue cleared;
        int frameCount;
        int valueCount;
        int depth;

        // Act
        try
        {
            context.EnsureCapacity(0, 1, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(new(outerFunction, 0, 0) { Pc = 17 });
            context.PushValue(WasmValue.FromI32(42));
            context.EnsureCapacity(3, 3, new(WasmProcessingStage.Invoke));
            context.TryEnterCall();
            context.PushFrame(new(function, 1, 3));
            context.PushValue(WasmValue.FromI32(1));
            context.PushValue(WasmValue.FromI32(2));
            context.PushValue(WasmValue.FromExternRef(new object()));
            context.PushValue(WasmValue.FromExternRef(reference));
            context.PushValue(WasmValue.FromI64(123));
            result = Interpreter.Return(context, in instruction);
            outerFrame = context.GetFrame(0);
            removedFrame = context.GetFrame(1);
            outerValue = context.GetValue(0);
            first = context.GetValue(1);
            second = context.GetValue(2);
            cleared = context.GetValue(4);
            frameCount = context.FrameCount;
            valueCount = context.ValueCount;
            depth = context.CallDepth;
        }
        finally
        {
            context.Restore(0, 0, 0);
            context.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(found).IsTrue();
            await Assert.That(descriptor.Immediate).IsEqualTo(ImmediateKind.None);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.FunctionEnd);
            await Assert.That(descriptor.StackEffect).IsEqualTo(StackEffectKind.FunctionEnd);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values).IsEmpty();
            await Assert.That(outerFrame.Function).IsSameReferenceAs(outerFunction);
            await Assert.That(outerFrame.Pc).IsEqualTo(17);
            await Assert.That(removedFrame.Function).IsNull();
            await Assert.That(outerValue.AsI32()).IsEqualTo(42);
            await Assert.That(first.AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(second.AsI64()).IsEqualTo(123);
            await Assert.That(cleared).IsEqualTo(default(WasmValue));
            await Assert.That(frameCount).IsEqualTo(1);
            await Assert.That(valueCount).IsEqualTo(3);
            await Assert.That(depth).IsEqualTo(1);
        }
    }
}
