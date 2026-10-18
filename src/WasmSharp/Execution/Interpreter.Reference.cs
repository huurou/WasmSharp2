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
        context.PushValue(
            WasmValue.FromFuncRef(
                context.CurrentFunction.Instance.Functions[(int)instruction.Index]
            )
        );
        return default;
    }

    /// <summary>
    /// 型付き参照を消費し、nullなら1、それ以外は0をi32として積む
    /// </summary>
    /// <param name="context">判定する参照operandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのref.is_null命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult RefIsNull(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var value = context.PopValue();
        var isNull =
            value.Kind == WasmValueKind.FuncRef
                ? value.AsFuncRef() is null
                : value.AsExternRef() is null;
        context.PushValue(WasmValue.FromI32(isNull ? 1 : 0));
        return default;
    }
}
