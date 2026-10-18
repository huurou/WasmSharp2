using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 所属instanceのtableから間接callの対象を解決し、関数型を照合して呼び出す
    /// </summary>
    /// <param name="context">呼び出し元の関数と引数、要素添字を保持する実行コンテキスト</param>
    /// <param name="instruction">期待する型とtableの添字を持つ検証済みのcall_indirect命令</param>
    /// <returns>呼び出しの成功、対象解決のtrap、または呼び出し前に検出した資源枯渇</returns>
    /// <exception cref="WasmImplementationLimitException">呼び出す定義関数に必要なフレーム数または値スタックの容量が配列の保持上限を超える場合</exception>
    /// <exception cref="InvalidOperationException">ホスト関数の結果がnullまたは宣言型と一致しない場合</exception>
    internal static ExecutionResult CallIndirect(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var function = context.CurrentFunction;
        var selector = unchecked((uint)context.PopValue().AsI32());
        if (
            !function
                .Instance.Tables[(int)instruction.SecondaryIndex]
                .TryGet(selector, out var value)
        )
        {
            return ExecutionResult.Trap(
                WasmTrapReason.TableOutOfBounds,
                function.FunctionIndex,
                instruction.ByteOffset,
                trapDiagnosticKind: TrapDiagnosticKind.UndefinedElement,
                tableElementIndex: selector
            );
        }
        var callee = value.AsFuncRef();
        if (callee is null)
        {
            return ExecutionResult.Trap(
                WasmTrapReason.UninitializedElement,
                function.FunctionIndex,
                instruction.ByteOffset,
                tableElementIndex: selector
            );
        }
        var expectedType = function.Instance.Module.Types[(int)instruction.Index];
        if (
            !expectedType.Parameters.SequenceEqual(callee.Type.Parameters)
            || !expectedType.Results.SequenceEqual(callee.Type.Results)
        )
        {
            return ExecutionResult.Trap(
                WasmTrapReason.IndirectCallTypeMismatch,
                function.FunctionIndex,
                instruction.ByteOffset
            );
        }
        return CallFunction(context, callee);
    }

    /// <summary>
    /// 実行中の関数の所属instanceから直接callの対象を解決して呼び出す
    /// </summary>
    /// <param name="context">呼び出し元の関数と引数を保持する実行コンテキスト</param>
    /// <param name="instruction">module全体の関数indexを持つ検証済みのcall命令</param>
    /// <returns>呼び出しの成功、または呼び出し前に検出した資源枯渇</returns>
    /// <exception cref="WasmImplementationLimitException">呼び出す定義関数に必要なフレーム数または値スタックの容量が配列の保持上限を超える場合</exception>
    /// <exception cref="InvalidOperationException">ホスト関数の結果がnullまたは宣言型と一致しない場合</exception>
    internal static ExecutionResult Call(InterpreterContext context, in Instruction instruction)
    {
        // importした定義関数も元instanceに所属したまま関数表へ置かれている。
        var callee = context.CurrentFunction.Instance.Functions[(int)instruction.Index];
        return CallFunction(context, callee);
    }

    /// <summary>
    /// 解決済みの関数を呼び出し、定義関数は同じ実行ループへフレームを追加する
    /// </summary>
    /// <param name="context">呼び出し元の関数と引数を保持する実行コンテキスト</param>
    /// <param name="callee">呼び出す関数実体</param>
    /// <returns>呼び出しの成功、または呼び出し前に検出した資源枯渇</returns>
    /// <remarks>
    /// importした定義関数は元の所属instanceを保つ。ホスト関数は同期的に実行し、引数を結果に置き換える。
    /// instance必須のホスト関数には、呼び出し元の定義関数の所属instanceを渡す
    /// </remarks>
    /// <exception cref="WasmImplementationLimitException">呼び出す定義関数に必要なフレーム数または値スタックの容量が配列の保持上限を超える場合</exception>
    /// <exception cref="InvalidOperationException">ホスト関数の結果がnullまたは宣言型と一致しない場合</exception>
    private static ExecutionResult CallFunction(InterpreterContext context, WasmFunction callee)
    {
        if (callee is not DefinedFunction function)
        {
            var argumentBase = context.ValueCount - callee.Type.Parameters.Length;
            var arguments = context.GetValues(argumentBase, callee.Type.Parameters.Length);
            var results = RunHost(context, callee, context.CurrentFunction.Instance, arguments);
            if (results.Status != ExecutionStatus.Success)
            {
                return results;
            }

            context.Restore(context.FrameCount, argumentBase, context.CallDepth);
            foreach (var value in results.Values)
            {
                context.PushValue(value);
            }
            return default;
        }
        if (!context.TryEnterCall())
        {
            return ExhaustCallDepth(context, function);
        }
        // 呼び出し元のoperand末尾に積まれた引数を、そのまま呼び出し先の引数領域とする。
        var stackBase = context.ValueCount - function.Type.Parameters.Length;
        EnsureFrameCapacity(context, function, stackBase);
        context.EnterFrame(function, stackBase);
        return default;
    }
}
