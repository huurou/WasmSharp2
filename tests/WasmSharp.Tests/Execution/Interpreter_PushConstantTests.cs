using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_PushConstantTests
{
    [Test]
    [Arguments(0x41, ImmediateKind.I32, StackEffectKind.PushI32)]
    [Arguments(0x42, ImmediateKind.I64, StackEffectKind.PushI64)]
    [Arguments(0x43, ImmediateKind.F32Bits, StackEffectKind.PushF32)]
    [Arguments(0x44, ImmediateKind.F64Bits, StackEffectKind.PushF64)]
    public async Task 定数命令を読み実行する_生成情報と元位置を保ち型とビット列を積む(
        int opcode,
        ImmediateKind immediate,
        StackEffectKind stackEffect
    )
    {
        // Arrange
        var value = opcode switch
        {
            0x41 => WasmValue.FromI32(int.MinValue),
            0x42 => WasmValue.FromI64(long.MinValue),
            0x43 => WasmValue.FromF32Bits(0xFFC12345),
            _ => WasmValue.FromF64Bits(0xFFF8123456789ABC),
        };
        var found = InstructionSet.TryGet(new(0, (uint)opcode), out var descriptor);
        var code = new FunctionCode(
            [new(descriptor.ExecutionOpcode!.Value, value, 12345678901)],
            1
        );
        var module = new WasmModule([new([], [value.Kind])], [new(0, 30, [], [])], [], 40)
        {
            FunctionCodes = [code],
        };
        var function = new WasmInstance(module, WasmExecutionOptions.Default).Functions[0];
        var context = WasmExecutionContext.Enter(WasmExecutionOptions.Default, out var isOutermost);
        Instruction instruction;
        ExecutionResult result;
        WasmValue actual;
        int pc;

        // Act
        try
        {
            context.EnsureCapacity(
                0,
                function.Code.MaxOperandStack,
                new(WasmProcessingStage.Invoke)
            );
            context.PushFrame(new(function, 0, 0));
            instruction = context.ReadNextInstruction();
            result = Interpreter.PushConstant(context, in instruction);
            actual = context.GetValue(0);
            pc = context.GetFrame(0).Pc;
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
            await Assert.That(descriptor.Immediate).IsEqualTo(immediate);
            await Assert.That(descriptor.StackEffect).IsEqualTo(stackEffect);
            await Assert.That(descriptor.Validation).IsEqualTo(ValidationRule.Constant);
            await Assert.That(function.Code).IsSameReferenceAs(code);
            await Assert.That(instruction.ByteOffset).IsEqualTo(12345678901);
            await Assert.That(pc).IsEqualTo(1);
            await Assert.That(result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(result.Values).IsEmpty();
            await Assert.That(actual).IsEqualTo(value);
        }
    }
}
