using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 現在の定義関数が属するinstanceの関数を同じ実体の参照として積む
    /// </summary>
    /// <param name="context">現在の定義関数を持つ実行コンテキスト</param>
    /// <param name="instruction">関数添字を持つ検証済みのref.func命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult RefFunc(InterpreterContext context, in Instruction instruction)
    {
        context.PushValue(WasmValue.FromFuncRef(context.CurrentFunction.Instance.Functions[(int)instruction.Index]));
        return default;
    }

    /// <summary>
    /// 型付き参照を消費し、nullなら1、それ以外は0をi32として積む
    /// </summary>
    /// <param name="context">判定する参照operandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのref.is_null命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult RefIsNull(InterpreterContext context, in Instruction instruction)
    {
        context.PushValue(WasmValue.FromI32(context.PopValue().IsNullReference ? 1 : 0));
        return default;
    }

    /// <summary>
    /// 非nullの参照をそのまま残し、nullの場合はtrapを返す
    /// </summary>
    internal static ExecutionResult RefAsNonNull(InterpreterContext context, in Instruction instruction)
    {
        if (context.PeekValue().IsNullReference)
        {
            return ExecutionResult.Trap(
                WasmTrapReason.NullReference,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
        }
        return default;
    }

    /// <summary>
    /// i32の下位31ビットから非nullのi31参照を作る
    /// </summary>
    internal static ExecutionResult RefI31(InterpreterContext context, in Instruction instruction)
    {
        context.PushValue(ConstantReferenceOperation.RefI31.Evaluate(context.PopValue()));
        return default;
    }

    /// <summary>
    /// i31参照のビット列を符号拡張したi32を積む
    /// </summary>
    internal static ExecutionResult I31GetS(InterpreterContext context, in Instruction instruction)
    {
        return GetI31(context, instruction, true);
    }

    /// <summary>
    /// i31参照のビット列をゼロ拡張したi32を積む
    /// </summary>
    internal static ExecutionResult I31GetU(InterpreterContext context, in Instruction instruction)
    {
        return GetI31(context, instruction, false);
    }

    private static ExecutionResult GetI31(InterpreterContext context, in Instruction instruction, bool signed)
    {
        var value = context.PopValue();
        if (value.IsNullReference)
        {
            return ExecutionResult.Trap(
                WasmTrapReason.NullI31Reference,
                context.CurrentFunction.FunctionIndex,
                instruction.ByteOffset
            );
        }
        context.PushValue(WasmValue.FromI32(signed ? value.AsI31Signed() : value.AsI31Unsigned()));
        return default;
    }
}
