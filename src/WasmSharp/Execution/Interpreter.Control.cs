namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 値と実行状態を変更せず、実行ループへ戻る
    /// </summary>
    /// <param name="context">現在の実行コンテキスト</param>
    /// <param name="instruction">検証済みのnopまたは制御入口のmarker</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult NoOp(InterpreterContext context, in Instruction instruction)
    {
        return default;
    }

    /// <summary>
    /// 指定した分岐先へ保持値を移して分岐する
    /// </summary>
    /// <param name="context">現在の関数とoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">分岐情報の添字を持つ検証済みの命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult Branch(InterpreterContext context, in Instruction instruction)
    {
        context.Branch(context.GetBranchTarget(instruction.Index));
        return default;
    }

    /// <summary>
    /// 条件を消費し、0なら指定した分岐先へ分岐する
    /// </summary>
    /// <param name="context">ifの条件と入口引数を持つ実行コンテキスト</param>
    /// <param name="instruction">elseまたはif終了への分岐情報を持つ検証済みの命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult BranchIfZero(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        if (context.PopValue().AsI32() == 0)
        {
            context.Branch(context.GetBranchTarget(instruction.Index));
        }
        return default;
    }

    /// <summary>
    /// 条件を消費し、非ゼロなら指定した分岐先へ分岐する
    /// </summary>
    /// <param name="context">条件と分岐値を持つ実行コンテキスト</param>
    /// <param name="instruction">分岐情報の添字を持つ検証済みのbr_if命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult BranchIf(InterpreterContext context, in Instruction instruction)
    {
        if (context.PopValue().AsI32() != 0)
        {
            context.Branch(context.GetBranchTarget(instruction.Index));
        }
        return default;
    }

    /// <summary>
    /// 符号なしの選択値で候補または既定の分岐先へ分岐する
    /// </summary>
    /// <param name="context">選択値と分岐値を持つ実行コンテキスト</param>
    /// <param name="instruction">候補列の開始添字と既定を除く候補数を持つ検証済みのbr_table命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult BranchTable(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var selector = unchecked((uint)context.PopValue().AsI32());
        var candidate =
            selector < (uint)instruction.TargetCount ? selector : (uint)instruction.TargetCount;
        context.Branch(context.GetBranchTarget(instruction.Index + candidate));
        return default;
    }

    /// <summary>
    /// 条件と両候補を消費し、選択した値を型とbit列を保って積む
    /// </summary>
    /// <param name="context">第一候補、第二候補、条件の順にoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのselect命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult Select(InterpreterContext context, in Instruction instruction)
    {
        var condition = context.PopValue().AsI32();
        var second = context.PopValue();
        var first = context.PopValue();
        context.PushValue(condition != 0 ? first : second);
        return default;
    }
}
