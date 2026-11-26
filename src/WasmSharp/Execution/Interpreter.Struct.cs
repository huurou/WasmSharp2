using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    internal static ExecutionResult StructNew(InterpreterContext context, in Instruction instruction)
    {
        var type = (WasmStructType)context.CurrentFunction.Instance.Module.Types[(int)instruction.Index];
        var firstField = context.ValueCount - type.Fields.Length;
        var value = WasmStruct.Create(type, context.GetValues(firstField, type.Fields.Length));
        context.Restore(context.FrameCount, firstField, context.CallDepth);
        context.PushValue(value);
        return default;
    }

    internal static ExecutionResult StructNewDefault(InterpreterContext context, in Instruction instruction)
    {
        var type = (WasmStructType)context.CurrentFunction.Instance.Module.Types[(int)instruction.Index];
        context.PushValue(WasmStruct.CreateDefault(type));
        return default;
    }

    internal static ExecutionResult StructSet(InterpreterContext context, in Instruction instruction)
    {
        var value = context.PopValue();
        var reference = context.PopValue();
        if (reference.IsNullReference)
        {
            return NullStructReference(context, instruction);
        }
        var structure = reference.AsStruct();
        structure.SetField((int)instruction.SecondaryIndex, value);
        return default;
    }

    internal static ExecutionResult StructGet(InterpreterContext context, in Instruction instruction)
    {
        return GetStructField(context, instruction, signed: false);
    }

    internal static ExecutionResult StructGetSigned(InterpreterContext context, in Instruction instruction)
    {
        return GetStructField(context, instruction, signed: true);
    }

    private static ExecutionResult NullStructReference(InterpreterContext context, in Instruction instruction)
    {
        return ExecutionResult.Trap(
            WasmTrapReason.NullStructReference,
            context.CurrentFunction.FunctionIndex,
            instruction.ByteOffset
        );
    }

    private static ExecutionResult GetStructField(InterpreterContext context, in Instruction instruction, bool signed)
    {
        var reference = context.PopValue();
        if (reference.IsNullReference)
        {
            return NullStructReference(context, instruction);
        }
        var structure = reference.AsStruct();
        context.PushValue(structure.GetField((int)instruction.SecondaryIndex, signed));
        return default;
    }
}
