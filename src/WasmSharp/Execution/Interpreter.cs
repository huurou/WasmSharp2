using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 線形命令を実行するインタープリタ
/// </summary>
internal static partial class Interpreter
{
    /// <summary>
    /// 今回の関数入口から実行し、終了時に呼び出し前のスタックと深さへ戻す
    /// </summary>
    internal static ExecutionResult Run(
        WasmExecutionContext context,
        WasmFunction function,
        ReadOnlySpan<WasmValue> arguments,
        WasmProcessingStage stage
    )
    {
        var frameCount = context.FrameCount;
        var valueCount = context.ValueCount;
        var callDepth = context.CallDepth;
        try
        {
            if (!context.TryEnterCall())
            {
                return ExecutionResult.Exhaustion(
                    WasmExhaustionReason.CallDepthLimit,
                    context.MaxCallDepth,
                    function.FunctionIndex,
                    function.Definition.BodyOffset
                );
            }
            // 検証済みの最小経路は引数とlocalsが0なので、両方の開始位置が一致する。
            context.EnsureCapacity(
                valueCount,
                function.Code.MaxOperandStack,
                new(stage, function.Definition.BodyOffset, function.FunctionIndex)
            );
            context.PushFrame(new(function, valueCount, valueCount));
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
        }
    }

    /// <summary>
    /// 定数の型とビット列を保ったまま確保済みのoperand領域へ積む
    /// </summary>
    internal static ExecutionResult PushConstant(
        WasmExecutionContext context,
        in Instruction instruction
    )
    {
        context.PushValue(instruction.Immediate);
        return default;
    }

    /// <summary>
    /// 現在の関数の結果を保持してフレームを終了する
    /// </summary>
    internal static ExecutionResult Return(WasmExecutionContext context, in Instruction instruction)
    {
        context.CompleteFrame();
        return default;
    }
}
