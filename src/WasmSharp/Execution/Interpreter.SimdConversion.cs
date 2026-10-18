namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// f32x4の各laneを符号付きi32へ飽和変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4TruncSatF32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4TruncSatF32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            32,
            static x =>
                unchecked(
                    (uint)TruncateSaturatingI32(BitConverter.UInt32BitsToSingle((uint)x), true)
                )
        );
    }

    /// <summary>
    /// f32x4の各laneを符号なしi32へ飽和変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4TruncSatF32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4TruncSatF32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            32,
            static x =>
                unchecked(
                    (uint)TruncateSaturatingI32(BitConverter.UInt32BitsToSingle((uint)x), false)
                )
        );
    }

    /// <summary>
    /// f64x2の各laneを符号付きi32へ飽和変換し上位2laneをゼロにする
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4TruncSatF64x2SZero命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4TruncSatF64x2SZero(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            64,
            32,
            static x =>
                unchecked((uint)TruncateSaturatingI32(BitConverter.UInt64BitsToDouble(x), true))
        );
    }

    /// <summary>
    /// f64x2の各laneを符号なしi32へ飽和変換し上位2laneをゼロにする
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4TruncSatF64x2UZero命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4TruncSatF64x2UZero(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            64,
            32,
            static x =>
                unchecked((uint)TruncateSaturatingI32(BitConverter.UInt64BitsToDouble(x), false))
        );
    }

    /// <summary>
    /// i32x4の各laneを符号付きとしてf32へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4ConvertI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4ConvertI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            32,
            static x => BitConverter.SingleToUInt32Bits((float)unchecked((int)x))
        );
    }

    /// <summary>
    /// i32x4の各laneを符号なしとしてf32へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4ConvertI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4ConvertI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            32,
            static x => BitConverter.SingleToUInt32Bits((float)(uint)x)
        );
    }

    /// <summary>
    /// i32x4の下位2laneを符号付きとしてf64へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2ConvertLowI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2ConvertLowI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            64,
            static x => BitConverter.DoubleToUInt64Bits((double)unchecked((int)x))
        );
    }

    /// <summary>
    /// i32x4の下位2laneを符号なしとしてf64へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2ConvertLowI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2ConvertLowI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            64,
            static x => BitConverter.DoubleToUInt64Bits((double)(uint)x)
        );
    }

    /// <summary>
    /// f64x2をf32へ丸めてNaN結果を補正し上位2laneをゼロにする
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4DemoteF64x2Zero命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4DemoteF64x2Zero(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            64,
            32,
            static x => F32ArithmeticResult((float)BitConverter.UInt64BitsToDouble(x)).AsF32Bits()
        );
    }

    /// <summary>
    /// f32x4の下位2laneをf64へ正確に拡張しNaN結果を補正する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2PromoteLowF32x4命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2PromoteLowF32x4(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdConversion(
            context,
            32,
            64,
            static x =>
                F64ArithmeticResult((double)BitConverter.UInt32BitsToSingle((uint)x)).AsF64Bits()
        );
    }

    /// <summary>
    /// 変換元のvectorを取り出し指定幅のlaneへ変換した結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="inputBitWidth">変換元のlane幅</param>
    /// <param name="outputBitWidth">変換先のlane幅</param>
    /// <param name="operation">入力laneのbit列を出力laneのbit列へ変換する純粋な演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdConversion(
        InterpreterContext context,
        int inputBitWidth,
        int outputBitWidth,
        Func<ulong, ulong> operation
    )
    {
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(
            MapSimdConversion(vector, inputBitWidth, outputBitWidth, operation).ToValue()
        );
        return default;
    }

    /// <summary>
    /// 入出力のlane数が重なる下位laneを変換し未使用の上位laneはゼロにする
    /// </summary>
    /// <param name="vector">変換元のビット列</param>
    /// <param name="inputBitWidth">入力laneの読み取り幅</param>
    /// <param name="outputBitWidth">出力laneの書き込み幅</param>
    /// <param name="operation">入力laneのbit列を出力laneのbit列へ変換する純粋な演算</param>
    /// <returns>変換後のビット列</returns>
    private static SimdValue MapSimdConversion(
        SimdValue vector,
        int inputBitWidth,
        int outputBitWidth,
        Func<ulong, ulong> operation
    )
    {
        var result = default(SimdValue);
        var laneCount = Math.Min(128 / inputBitWidth, 128 / outputBitWidth);
        for (var laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            result = result.WithLane(
                outputBitWidth,
                laneIndex,
                operation(vector.GetLane(inputBitWidth, laneIndex))
            );
        }

        return result;
    }
}
