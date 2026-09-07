using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 関数の公開呼び出しでコンテキストの寿命と実行失敗の例外化を扱う
/// </summary>
internal static class ExecutionBoundary
{
    /// <summary>
    /// 所属instanceのポリシーで同期実行に入り、最外側の終了時にコンテキストを解除する
    /// </summary>
    internal static WasmResults Invoke(
        WasmFunction function,
        ReadOnlySpan<WasmValue> arguments,
        WasmProcessingStage stage
    )
    {
        var context = WasmExecutionContext.Enter(
            function.Instance.ExecutionOptions,
            out var isOutermost
        );
        try
        {
            var result = Interpreter.Run(context, function, arguments, stage);
            ThrowIfFailed(result, stage);
            return new WasmResults(result.Values.AsSpan());
        }
        finally
        {
            context.Exit(isOutermost);
        }
    }

    /// <summary>
    /// 実行失敗の原因と元位置に呼び出し元の処理段階を付け、専用例外へ変換する
    /// </summary>
    internal static void ThrowIfFailed(ExecutionResult result, WasmProcessingStage stage)
    {
        if (result.Status == ExecutionStatus.Trap)
        {
            throw new WasmTrapException(
                "Wasmの実行中にtrapが発生しました。",
                result.TrapReason!.Value,
                new WasmFailureLocation(stage, result.ByteOffset, result.FunctionIndex)
            );
        }
        if (result.Status == ExecutionStatus.Exhaustion)
        {
            throw new WasmExhaustionException(
                "Wasmの呼び出し深さが上限に達しました。",
                result.ExhaustionReason!.Value,
                result.Limit!.Value,
                new WasmFailureLocation(stage, result.ByteOffset, result.FunctionIndex)
            );
        }
    }
}
