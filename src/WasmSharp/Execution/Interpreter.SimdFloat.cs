namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// f32x4の各laneの符号bitだけをゼロにする
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Abs(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatUnary(context, 32, static x => x & 0x7FFFFFFFUL);
    }

    /// <summary>
    /// f32x4の各laneの符号bitだけを反転する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Neg(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatUnary(context, 32, static x => x ^ 0x80000000UL);
    }

    /// <summary>
    /// f32x4の各laneを同精度で加算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Add(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            32,
            static (x, y) =>
                F32ArithmeticResult(
                        BitConverter.UInt32BitsToSingle((uint)x)
                            + BitConverter.UInt32BitsToSingle((uint)y)
                    )
                    .AsF32Bits()
        );
    }

    /// <summary>
    /// f32x4の各laneをoperand順に同精度で減算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Sub(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            32,
            static (x, y) =>
                F32ArithmeticResult(
                        BitConverter.UInt32BitsToSingle((uint)x)
                            - BitConverter.UInt32BitsToSingle((uint)y)
                    )
                    .AsF32Bits()
        );
    }

    /// <summary>
    /// f32x4の各laneを同精度で乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Mul(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            32,
            static (x, y) =>
                F32ArithmeticResult(
                        BitConverter.UInt32BitsToSingle((uint)x)
                            * BitConverter.UInt32BitsToSingle((uint)y)
                    )
                    .AsF32Bits()
        );
    }

    /// <summary>
    /// f32x4の各laneをoperand順に同精度で除算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Div命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Div(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            32,
            static (x, y) =>
                F32ArithmeticResult(
                        BitConverter.UInt32BitsToSingle((uint)x)
                            / BitConverter.UInt32BitsToSingle((uint)y)
                    )
                    .AsF32Bits()
        );
    }

    /// <summary>
    /// f32x4の各laneの平方根を同精度で求める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32x4Sqrt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32x4Sqrt(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdFloatUnary(
            context,
            32,
            static x =>
                F32ArithmeticResult(MathF.Sqrt(BitConverter.UInt32BitsToSingle((uint)x)))
                    .AsF32Bits()
        );
    }

    /// <summary>
    /// f64x2の各laneの符号bitだけをゼロにする
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Abs(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatUnary(context, 64, static x => x & 0x7FFFFFFFFFFFFFFFUL);
    }

    /// <summary>
    /// f64x2の各laneの符号bitだけを反転する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Neg(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatUnary(context, 64, static x => x ^ 0x8000000000000000UL);
    }

    /// <summary>
    /// f64x2の各laneを同精度で加算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Add(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            64,
            static (x, y) =>
                F64ArithmeticResult(
                        BitConverter.UInt64BitsToDouble(x) + BitConverter.UInt64BitsToDouble(y)
                    )
                    .AsF64Bits()
        );
    }

    /// <summary>
    /// f64x2の各laneをoperand順に同精度で減算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Sub(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            64,
            static (x, y) =>
                F64ArithmeticResult(
                        BitConverter.UInt64BitsToDouble(x) - BitConverter.UInt64BitsToDouble(y)
                    )
                    .AsF64Bits()
        );
    }

    /// <summary>
    /// f64x2の各laneを同精度で乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Mul(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            64,
            static (x, y) =>
                F64ArithmeticResult(
                        BitConverter.UInt64BitsToDouble(x) * BitConverter.UInt64BitsToDouble(y)
                    )
                    .AsF64Bits()
        );
    }

    /// <summary>
    /// f64x2の各laneをoperand順に同精度で除算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Div命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Div(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdFloatBinary(
            context,
            64,
            static (x, y) =>
                F64ArithmeticResult(
                        BitConverter.UInt64BitsToDouble(x) / BitConverter.UInt64BitsToDouble(y)
                    )
                    .AsF64Bits()
        );
    }

    /// <summary>
    /// f64x2の各laneの平方根を同精度で求める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64x2Sqrt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64x2Sqrt(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdFloatUnary(
            context,
            64,
            static x =>
                F64ArithmeticResult(Math.Sqrt(BitConverter.UInt64BitsToDouble(x))).AsF64Bits()
        );
    }

    /// <summary>
    /// 単項浮動小数点演算のoperandを取り出し結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">laneのビット列へ適用する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdFloatUnary(
        InterpreterContext context,
        int bitWidth,
        Func<ulong, ulong> operation
    )
    {
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdFloatUnary(vector, bitWidth, operation).ToValue());
        return default;
    }

    /// <summary>
    /// 二項浮動小数点演算のoperandを逆順に取り出し結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">左右のlaneのビット列へ適用する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdFloatBinary(
        InterpreterContext context,
        int bitWidth,
        Func<ulong, ulong, ulong> operation
    )
    {
        var right = SimdValue.FromValue(context.PopValue());
        var left = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdFloatBinary(left, right, bitWidth, operation).ToValue());
        return default;
    }

    /// <summary>
    /// 各laneへ単項演算を適用し新しいvectorを構築する
    /// </summary>
    /// <param name="vector">入力のビット列</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">laneの符号bit操作または純粋な浮動小数点演算</param>
    /// <returns>演算後のビット列</returns>
    private static SimdValue MapSimdFloatUnary(
        SimdValue vector,
        int bitWidth,
        Func<ulong, ulong> operation
    )
    {
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            result = result.WithLane(
                bitWidth,
                laneIndex,
                operation(vector.GetLane(bitWidth, laneIndex))
            );
        }

        return result;
    }

    /// <summary>
    /// 左右の各laneへ二項演算を適用し新しいvectorを構築する
    /// </summary>
    /// <param name="left">左operandのビット列</param>
    /// <param name="right">右operandのビット列</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">左右のlaneへ適用する純粋な浮動小数点演算</param>
    /// <returns>演算後のビット列</returns>
    private static SimdValue MapSimdFloatBinary(
        SimdValue left,
        SimdValue right,
        int bitWidth,
        Func<ulong, ulong, ulong> operation
    )
    {
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            var bits = operation(
                left.GetLane(bitWidth, laneIndex),
                right.GetLane(bitWidth, laneIndex)
            );
            result = result.WithLane(bitWidth, laneIndex, bits);
        }

        return result;
    }
}
