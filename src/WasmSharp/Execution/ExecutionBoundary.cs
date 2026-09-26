using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 関数の公開呼び出しでコンテキストの寿命と実行失敗の例外化を扱う
/// </summary>
internal static class ExecutionBoundary
{
    /// <summary>
    /// 定義関数の入口だけでコンテキストを開き、ホスト処理へのアクセス先と実行ポリシーを分ける
    /// </summary>
    internal static WasmResults Invoke(
        WasmFunction function,
        WasmInstance? explicitInstance,
        ReadOnlySpan<WasmValue> arguments
    )
    {
        if (function is not DefinedFunction definedFunction)
        {
            var hostResult = Interpreter.RunHost(
                InterpreterContext.Current,
                function,
                explicitInstance,
                arguments
            );

            ThrowIfFailed(hostResult, WasmProcessingStage.Invoke);

            return new WasmResults(hostResult.Values.AsSpan());
        }

        var context = InterpreterContext.Enter(
            definedFunction.Instance.ExecutionOptions,
            out var isOutermost
        );
        try
        {
            var result = Interpreter.Run(
                context,
                definedFunction,
                arguments,
                WasmProcessingStage.Invoke
            );

            ThrowIfFailed(result, WasmProcessingStage.Invoke);

            return new WasmResults(result.Values.AsSpan());
        }
        finally
        {
            InterpreterContext.Exit(isOutermost);
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
                "Wasmの実行資源が上限に達しました。",
                result.ExhaustionReason!.Value,
                result.Limit,
                new WasmFailureLocation(stage, result.ByteOffset, result.FunctionIndex)
            );
        }
    }
}
