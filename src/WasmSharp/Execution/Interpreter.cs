using System.Runtime.CompilerServices;
using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 線形命令を実行するインタープリタ
/// </summary>
internal static partial class Interpreter
{
    /// <summary>
    /// ホスト呼び出しを進行中の実行の深さへ数え、終了時にその1段を解放する
    /// </summary>
    internal static ExecutionResult RunHost(
        InterpreterContext? context,
        WasmFunction function,
        WasmInstance? instance,
        ReadOnlySpan<WasmValue> arguments
    )
    {
        if (context is not null && !context.TryEnterCall())
        {
            return ExhaustHost(context, WasmExhaustionReason.CallDepthLimit, context.MaxCallDepth);
        }
        try
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return ExhaustHost(context, WasmExhaustionReason.HostStackLimit, null);
            }
            return ExecutionResult.Success(InvokeHost(function, instance, arguments).Values);
        }
        finally
        {
            context?.ExitCall();
        }
    }

    /// <summary>
    /// ホストへの入場失敗に、進行中のWasmのcall命令の位置を付ける
    /// </summary>
    /// <remarks>
    /// ホストから別ホストへの公開Invokeでも、外側のWasmのcall命令を使う。Wasmフレームがなければ位置はnull
    /// </remarks>
    private static ExecutionResult ExhaustHost(
        InterpreterContext? context,
        WasmExhaustionReason reason,
        int? limit
    )
    {
        uint? functionIndex = null;
        long? byteOffset = null;
        if (context is { FrameCount: > 0 })
        {
            var frame = context.GetFrame(context.FrameCount - 1);
            functionIndex = frame.Function.FunctionIndex;
            byteOffset = frame.Function.Code.Instructions[frame.Pc - 1].ByteOffset;
        }
        return ExecutionResult.Exhaustion(reason, limit, functionIndex, byteOffset);
    }

    /// <summary>
    /// 呼び出し専用の引数でホスト処理を実行し、宣言型に一致する所有済みの結果を返す
    /// </summary>
    internal static WasmResults InvokeHost(
        WasmFunction function,
        WasmInstance? instance,
        ReadOnlySpan<WasmValue> arguments
    )
    {
        var copiedArguments = arguments.ToArray();
        var result = function switch
        {
            HostFunction host => host.Callback(copiedArguments),
            InstanceHostFunction host => host.Callback(instance!, copiedArguments),
            _ => throw new InvalidOperationException("ホスト関数ではありません。"),
        };
        if (result is null || result.Values.Length != function.Type.Results.Length)
        {
            throw new InvalidOperationException("ホスト関数の結果の個数が関数型と一致しません。");
        }
        for (var i = 0; i < result.Values.Length; i++)
        {
            if (result.Values[i].Kind != function.Type.Results[i])
            {
                throw new InvalidOperationException("ホスト関数の結果の型が関数型と一致しません。");
            }
        }
        return result;
    }

    /// <summary>
    /// 今回の関数入口から実行し、終了時に呼び出し前のスタック・深さ・処理段階へ戻す
    /// </summary>
    internal static ExecutionResult Run(
        InterpreterContext context,
        DefinedFunction function,
        ReadOnlySpan<WasmValue> arguments,
        WasmProcessingStage stage
    )
    {
        var frameCount = context.FrameCount;
        var valueCount = context.ValueCount;
        var callDepth = context.CallDepth;
        var outerStage = context.Stage;
        try
        {
            context.Stage = stage;
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return ExecutionResult.Exhaustion(
                    WasmExhaustionReason.HostStackLimit,
                    null,
                    function.FunctionIndex,
                    function.Definition.BodyOffset
                );
            }
            if (!context.TryEnterCall())
            {
                return ExhaustCallDepth(context, function);
            }
            EnsureFrameCapacity(context, function, valueCount);
            foreach (var argument in arguments)
            {
                context.PushValue(argument);
            }
            context.EnterFrame(function, valueCount);
            var result = RunLoop(context, frameCount);
            if (result.Status != ExecutionStatus.Success)
            {
                return result;
            }
            return ExecutionResult.Success(
                context.CopyValues(valueCount, function.Type.Results.Length)
            );
        }
        finally
        {
            context.Restore(frameCount, valueCount, callDepth);
            context.Stage = outerStage;
        }
    }

    /// <summary>
    /// 呼び出し深さの上限到達を、入場しようとした定義関数の位置で返す
    /// </summary>
    /// <param name="context">上限を固定した実行コンテキスト</param>
    /// <param name="function">入場しようとした定義関数</param>
    /// <returns>呼び出し深さの上限到達を表す実行結果</returns>
    private static ExecutionResult ExhaustCallDepth(
        InterpreterContext context,
        DefinedFunction function
    )
    {
        return ExecutionResult.Exhaustion(
            WasmExhaustionReason.CallDepthLimit,
            context.MaxCallDepth,
            function.FunctionIndex,
            function.Definition.BodyOffset
        );
    }

    /// <summary>
    /// 引数の開始位置から、引数・追加locals・operandの合計に必要な容量を確保する
    /// </summary>
    /// <param name="context">フレームを追加する実行コンテキスト</param>
    /// <param name="function">開始する定義関数</param>
    /// <param name="stackBase">共有値スタック上の引数の開始位置</param>
    /// <exception cref="WasmImplementationLimitException">必要数が配列の保持上限を超える場合</exception>
    private static void EnsureFrameCapacity(
        InterpreterContext context,
        DefinedFunction function,
        int stackBase
    )
    {
        // 追加localsは最大でuint範囲のため、各数を広げて加算してから保持上限と比べる。
        context.EnsureCapacity(
            (ulong)stackBase + (ulong)function.Type.Parameters.Length + function.Code.LocalCount,
            function.Code.MaxOperandStack,
            new WasmFailureLocation(
                context.Stage,
                function.Definition.BodyOffset,
                function.FunctionIndex
            )
        );
    }

    /// <summary>
    /// 実行中の関数の所属instanceから直接callの対象を解決し、定義関数は同じ実行ループへフレームを追加する
    /// </summary>
    internal static ExecutionResult Call(InterpreterContext context, in Instruction instruction)
    {
        // importした定義関数も元instanceに所属したまま関数表へ置かれている。
        var callee = context.CurrentFunction.Instance.Functions[(int)instruction.Index];
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

    /// <summary>
    /// 定数の型とビット列を保ったまま確保済みのoperand領域へ積む
    /// </summary>
    internal static ExecutionResult PushConstant(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        context.PushValue(instruction.Immediate);
        return default;
    }

    /// <summary>
    /// 実行を中断し、実行中の関数のindexと命令位置を持つtrapを返す
    /// </summary>
    internal static ExecutionResult Unreachable(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecutionResult.Trap(
            WasmTrapReason.Unreachable,
            context.CurrentFunction.FunctionIndex,
            instruction.ByteOffset
        );
    }

    /// <summary>
    /// 最上位の値を1個取り除き、残る値を変更しない
    /// </summary>
    internal static ExecutionResult Drop(InterpreterContext context, in Instruction instruction)
    {
        context.PopValue();
        return default;
    }

    /// <summary>
    /// 指定したlocalの現在値を積む
    /// </summary>
    internal static ExecutionResult LocalGet(InterpreterContext context, in Instruction instruction)
    {
        context.PushValue(context.GetLocal(instruction.Index));
        return default;
    }

    /// <summary>
    /// 最上位の値を取り除いて指定したlocalへ設定する
    /// </summary>
    internal static ExecutionResult LocalSet(InterpreterContext context, in Instruction instruction)
    {
        context.SetLocal(instruction.Index, context.PopValue());
        return default;
    }

    /// <summary>
    /// 最上位の値を残したまま指定したlocalへ設定する
    /// </summary>
    internal static ExecutionResult LocalTee(InterpreterContext context, in Instruction instruction)
    {
        context.SetLocal(instruction.Index, context.PeekValue());
        return default;
    }

    /// <summary>
    /// 実行中の関数の所属instanceから指定globalの現在値を積む
    /// </summary>
    internal static ExecutionResult GlobalGet(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        context.PushValue(context.CurrentFunction.Instance.Globals[(int)instruction.Index].Value);
        return default;
    }

    /// <summary>
    /// 最上位の値を取り除き、実行中の関数の所属instanceの指定globalへ設定する
    /// </summary>
    internal static ExecutionResult GlobalSet(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        // 型と可変性は検証済みのため、ホスト操作向けの検査を重ねない。
        context
            .CurrentFunction.Instance.Globals[(int)instruction.Index]
            .SetValidatedValue(context.PopValue());
        return default;
    }

    /// <summary>
    /// 現在の関数の結果を保持してフレームを終了する
    /// </summary>
    internal static ExecutionResult Return(InterpreterContext context, in Instruction instruction)
    {
        context.CompleteFrame();
        return default;
    }
}
