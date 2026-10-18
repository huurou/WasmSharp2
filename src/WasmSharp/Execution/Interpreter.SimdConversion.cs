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
        return ExecuteSimdI32Conversion(
            context,
            32,
            static x => TruncateSaturatingI32(BitConverter.UInt32BitsToSingle((uint)x), true)
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
        return ExecuteSimdI32Conversion(
            context,
            32,
            static x => TruncateSaturatingI32(BitConverter.UInt32BitsToSingle((uint)x), false)
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
        return ExecuteSimdI32Conversion(
            context,
            64,
            static x => TruncateSaturatingI32(BitConverter.UInt64BitsToDouble(x), true)
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
        return ExecuteSimdI32Conversion(
            context,
            64,
            static x => TruncateSaturatingI32(BitConverter.UInt64BitsToDouble(x), false)
        );
    }

    /// <summary>
    /// 変換元のvectorを取り出しi32のlaneへ変換した結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="inputBitWidth">変換元のlane幅</param>
    /// <param name="operation">入力laneのbit列をi32のbit列へ変換する純粋な演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdI32Conversion(
        InterpreterContext context,
        int inputBitWidth,
        Func<ulong, int> operation
    )
    {
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdI32Conversion(vector, inputBitWidth, operation).ToValue());
        return default;
    }

    /// <summary>
    /// 各入力laneを32bitの結果laneへ変換し未使用の上位laneはゼロにする
    /// </summary>
    /// <param name="vector">変換元のビット列</param>
    /// <param name="inputBitWidth">入力laneの読み取り幅</param>
    /// <param name="operation">入力laneのbit列をi32のbit列へ変換する純粋な演算</param>
    /// <returns>変換後のビット列</returns>
    private static SimdValue MapSimdI32Conversion(
        SimdValue vector,
        int inputBitWidth,
        Func<ulong, int> operation
    )
    {
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / inputBitWidth; laneIndex++)
        {
            result = result.WithLane(
                32,
                laneIndex,
                unchecked((uint)operation(vector.GetLane(inputBitWidth, laneIndex)))
            );
        }

        return result;
    }
}
