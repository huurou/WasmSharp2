using System.Globalization;
using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 関数の公開呼び出しとstartでコンテキストの寿命と実行失敗の例外化を扱う
/// </summary>
internal static class ExecutionBoundary
{
    /// <summary>
    /// start関数を実行し、実行失敗をインスタンス化時の例外へ変換する
    /// </summary>
    /// <param name="executionContext">同期呼び出しの間で共有するコンテキスト</param>
    /// <param name="startInstance">startを宣言し、リソースとexportの構築が完了したinstance</param>
    /// <param name="function">引数も結果も持たないことを検証済みのstart関数</param>
    /// <remarks>
    /// 指定コンテキストで実行中なら深さと上限を共有し、未実行ならstartを持つinstanceの実行ポリシーで開始する。
    /// 定義関数は元の所属instanceで実行し、instanceを受け取るホスト関数にはstartを持つinstanceを渡す。
    /// 終了時は成功・失敗にかかわらず呼び出し前の実行状態へ戻すが、リソースの変更は取り消さない。
    /// ホスト処理が投げた例外は変換せず、そのまま伝播する
    /// </remarks>
    /// <exception cref="WasmTrapException">startの実行結果がtrapの場合 処理段階はInstantiate</exception>
    /// <exception cref="WasmExhaustionException">startの実行結果が資源枯渇の場合 処理段階はInstantiate</exception>
    /// <exception cref="WasmImplementationLimitException">実行に必要なフレーム数または値の数が保持上限を超える場合</exception>
    /// <exception cref="InvalidOperationException">ホスト関数の結果がnull、または宣言型と個数・型が異なる場合</exception>
    internal static void RunStart(WasmExecutionContext executionContext, WasmInstance startInstance, WasmFunction function)
    {
        var context = executionContext.Enter(startInstance.ExecutionOptions, out var isOutermost);
        var outerStage = context.Stage;
        try
        {
            context.Stage = WasmProcessingStage.Instantiate;
            var result = function is DefinedFunction definedFunction
                ? Interpreter.Run(context, definedFunction, [], WasmProcessingStage.Instantiate)
                : Interpreter.RunHost(executionContext, function, startInstance, []);

            ThrowIfFailed(result, WasmProcessingStage.Instantiate);
        }
        finally
        {
            context.Stage = outerStage;
            executionContext.Exit(isOutermost);
        }
    }

    /// <summary>
    /// 公開APIから関数を実行し、実行失敗を呼び出し時の例外へ変換する
    /// </summary>
    /// <param name="executionContext">同期呼び出しの間で共有するコンテキスト</param>
    /// <param name="function">呼び出す関数</param>
    /// <param name="explicitInstance">ホスト関数へ明示的に渡すinstance instance必須のホスト関数以外では未使用</param>
    /// <param name="arguments">呼び出し元で個数と型の照合を済ませた引数</param>
    /// <returns>実行スタックから独立して保持できる結果</returns>
    /// <remarks>
    /// 定義関数は元の所属instanceで実行する。指定コンテキストで未実行ならそのinstanceのポリシーで開始する。
    /// 実行中なら上限を共有する。ホスト関数だけの呼び出しでは新しい実行状態を作らない。
    /// 終了時は呼び出し前の実行状態へ戻す。ホスト処理が投げた例外は変換せず、そのまま伝播する
    /// </remarks>
    /// <exception cref="WasmTrapException">実行結果がtrapの場合 処理段階はInvoke</exception>
    /// <exception cref="WasmExhaustionException">実行結果が資源枯渇の場合 処理段階はInvoke</exception>
    /// <exception cref="WasmImplementationLimitException">実行に必要なフレーム数または値の数が保持上限を超える場合</exception>
    /// <exception cref="InvalidOperationException">ホスト関数の結果がnull、または宣言型と個数・型が異なる場合</exception>
    internal static WasmResults Invoke(
        WasmExecutionContext executionContext,
        WasmFunction function,
        WasmInstance? explicitInstance,
        ReadOnlySpan<WasmValue> arguments
    )
    {
        if (function is not DefinedFunction definedFunction)
        {
            var hostResult = Interpreter.RunHost(executionContext, function, explicitInstance, arguments);

            ThrowIfFailed(hostResult, WasmProcessingStage.Invoke);

            return new WasmResults(hostResult.Values.AsSpan());
        }

        var context = executionContext.Enter(definedFunction.Instance.ExecutionOptions, out var isOutermost);
        try
        {
            var result = Interpreter.Run(context, definedFunction, arguments, WasmProcessingStage.Invoke);

            ThrowIfFailed(result, WasmProcessingStage.Invoke);

            return new WasmResults(result.Values.AsSpan());
        }
        finally
        {
            executionContext.Exit(isOutermost);
        }
    }

    /// <summary>
    /// 実行失敗の原因と元位置に呼び出し元の処理段階を付け、専用例外へ変換する
    /// </summary>
    /// <param name="result">実行の成功、trap、資源枯渇のいずれかを表す結果</param>
    /// <param name="stage">生成する例外の位置情報に設定する処理段階</param>
    /// <remarks>成功時は何もしない。失敗時は原因・上限・関数index・バイト位置・セクションIDを保持する</remarks>
    /// <exception cref="WasmTrapException">結果がtrapの場合</exception>
    /// <exception cref="WasmExhaustionException">結果が資源枯渇の場合</exception>
    internal static void ThrowIfFailed(ExecutionResult result, WasmProcessingStage stage)
    {
        if (result.Status == ExecutionStatus.Trap)
        {
            throw new WasmTrapException(
                GetTrapMessage(result),
                result.TrapReason!.Value,
                new WasmFailureLocation(stage, result.ByteOffset, result.FunctionIndex, result.SectionId)
            );
        }
        if (result.Status == ExecutionStatus.Exhaustion)
        {
            throw new WasmExhaustionException(
                "call stack exhausted",
                result.ExhaustionReason!.Value,
                result.Limit,
                new WasmFailureLocation(stage, result.ByteOffset, result.FunctionIndex)
            );
        }
    }

    /// <summary>
    /// 実行中のtrapの結果から公開診断を選ぶ
    /// </summary>
    /// <param name="result">実行中に検出されたtrapの原因と診断情報</param>
    /// <returns>原因に対応する公開診断</returns>
    private static string GetTrapMessage(ExecutionResult result)
    {
        var elementIndexSuffix = result.TableElementIndex is { } index
            ? " " + index.ToString(CultureInfo.InvariantCulture)
            : "";
        return (result.TrapReason!.Value, result.TrapDiagnosticKind) switch
        {
            (WasmTrapReason.Unreachable, _) => "unreachable executed",
            (WasmTrapReason.IntegerDivideByZero, _) => "integer divide by zero",
            (WasmTrapReason.IntegerOverflow, _) => "integer overflow",
            (WasmTrapReason.InvalidConversionToInteger, _) => "invalid conversion to integer",
            (WasmTrapReason.MemoryOutOfBounds, _) => "out of bounds memory access",
            (WasmTrapReason.TableOutOfBounds, TrapDiagnosticKind.UndefinedElement) => "undefined element"
                + elementIndexSuffix,
            (WasmTrapReason.TableOutOfBounds, _) => "out of bounds table access",
            (WasmTrapReason.UninitializedElement, _) => "uninitialized element" + elementIndexSuffix,
            (WasmTrapReason.IndirectCallTypeMismatch, _) => "indirect call type mismatch",
            _ => "Wasmの実行中にtrapが発生しました。",
        };
    }
}
