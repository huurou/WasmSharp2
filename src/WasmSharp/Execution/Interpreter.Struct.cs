using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 宣言順に積まれたフィールド値を消費し、新しく生成した構造体の参照を積む
    /// </summary>
    internal static ExecutionResult StructNew(InterpreterContext context, in Instruction instruction)
    {
        var type = (WasmStructType)context.CurrentFunction.Instance.Module.Types[(int)instruction.Index];
        var firstField = context.ValueCount - type.Fields.Length;
        var value = WasmStruct.Create(type, context.GetValues(firstField, type.Fields.Length));
        context.TruncateValues(firstField);
        context.PushValue(value);
        return default;
    }

    /// <summary>
    /// 各フィールドを既定値で初期化した構造体の参照を積む
    /// </summary>
    internal static ExecutionResult StructNewDefault(InterpreterContext context, in Instruction instruction)
    {
        var type = (WasmStructType)context.CurrentFunction.Instance.Module.Types[(int)instruction.Index];
        context.PushValue(WasmStruct.CreateDefault(type));
        return default;
    }

    /// <summary>
    /// 検証済みの可変フィールドに値を書き込み、参照がnullならtrapを返す
    /// </summary>
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

    /// <summary>
    /// struct.getとstruct.get_uの値を読み出す。詰め込みフィールドは書き込み時にゼロ拡張している
    /// </summary>
    internal static ExecutionResult StructGet(InterpreterContext context, in Instruction instruction)
    {
        return GetStructField(context, instruction, signed: false);
    }

    /// <summary>
    /// 詰め込みフィールドを格納幅に応じて符号拡張し、i32の値を積む
    /// </summary>
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
