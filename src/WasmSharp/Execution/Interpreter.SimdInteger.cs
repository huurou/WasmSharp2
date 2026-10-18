namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 8bitの各laneで絶対値を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Abs(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(
            context,
            8,
            static x => (x & 0x80UL) == 0 ? x : unchecked(0UL - x)
        );
    }

    /// <summary>
    /// 8bitの各laneで負数化を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Neg(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(context, 8, static x => unchecked(0UL - x));
    }

    /// <summary>
    /// 8bitの各laneで加算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Add(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 8, static (x, y) => unchecked(x + y));
    }

    /// <summary>
    /// 8bitの各laneで減算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Sub(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 8, static (x, y) => unchecked(x - y));
    }

    /// <summary>
    /// 8bitの各laneで左シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Shl命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Shl(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerShift(context, 8, static (x, shift) => x << shift);
    }

    /// <summary>
    /// 8bitの各laneで符号付き右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16ShrS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16ShrS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(
            context,
            8,
            static (x, shift) => unchecked((ulong)((sbyte)x >> shift))
        );
    }

    /// <summary>
    /// 8bitの各laneでゼロ埋め右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16ShrU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16ShrU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(context, 8, static (x, shift) => x >> shift);
    }

    /// <summary>
    /// 16bitの各laneで絶対値を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Abs(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(
            context,
            16,
            static x => (x & 0x8000UL) == 0 ? x : unchecked(0UL - x)
        );
    }

    /// <summary>
    /// 16bitの各laneで負数化を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Neg(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(context, 16, static x => unchecked(0UL - x));
    }

    /// <summary>
    /// 16bitの各laneで加算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Add(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 16, static (x, y) => unchecked(x + y));
    }

    /// <summary>
    /// 16bitの各laneで減算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Sub(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 16, static (x, y) => unchecked(x - y));
    }

    /// <summary>
    /// 16bitの各laneで乗算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Mul(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 16, static (x, y) => unchecked(x * y));
    }

    /// <summary>
    /// 16bitの各laneで左シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8Shl命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8Shl(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerShift(context, 16, static (x, shift) => x << shift);
    }

    /// <summary>
    /// 16bitの各laneで符号付き右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ShrS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ShrS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(
            context,
            16,
            static (x, shift) => unchecked((ulong)((short)x >> shift))
        );
    }

    /// <summary>
    /// 16bitの各laneでゼロ埋め右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ShrU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ShrU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(context, 16, static (x, shift) => x >> shift);
    }

    /// <summary>
    /// 32bitの各laneで絶対値を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Abs(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(
            context,
            32,
            static x => (x & 0x80000000UL) == 0 ? x : unchecked(0UL - x)
        );
    }

    /// <summary>
    /// 32bitの各laneで負数化を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Neg(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(context, 32, static x => unchecked(0UL - x));
    }

    /// <summary>
    /// 32bitの各laneで加算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Add(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 32, static (x, y) => unchecked(x + y));
    }

    /// <summary>
    /// 32bitの各laneで減算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Sub(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 32, static (x, y) => unchecked(x - y));
    }

    /// <summary>
    /// 32bitの各laneで乗算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Mul(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 32, static (x, y) => unchecked(x * y));
    }

    /// <summary>
    /// 32bitの各laneで左シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4Shl命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4Shl(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerShift(context, 32, static (x, shift) => x << shift);
    }

    /// <summary>
    /// 32bitの各laneで符号付き右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ShrS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ShrS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(
            context,
            32,
            static (x, shift) => unchecked((ulong)((int)x >> shift))
        );
    }

    /// <summary>
    /// 32bitの各laneでゼロ埋め右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ShrU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ShrU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(context, 32, static (x, shift) => x >> shift);
    }

    /// <summary>
    /// 64bitの各laneで絶対値を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Abs(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(
            context,
            64,
            static x => (x & 0x8000000000000000UL) == 0 ? x : unchecked(0UL - x)
        );
    }

    /// <summary>
    /// 64bitの各laneで負数化を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Neg(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerUnary(context, 64, static x => unchecked(0UL - x));
    }

    /// <summary>
    /// 64bitの各laneで加算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Add(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 64, static (x, y) => unchecked(x + y));
    }

    /// <summary>
    /// 64bitの各laneで減算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Sub(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 64, static (x, y) => unchecked(x - y));
    }

    /// <summary>
    /// 64bitの各laneで乗算を行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Mul(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerBinary(context, 64, static (x, y) => unchecked(x * y));
    }

    /// <summary>
    /// 64bitの各laneで左シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2Shl命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2Shl(InterpreterContext context, in Instruction instruction)
    {
        return ExecuteSimdIntegerShift(context, 64, static (x, shift) => x << shift);
    }

    /// <summary>
    /// 64bitの各laneで符号付き右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ShrS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ShrS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(
            context,
            64,
            static (x, shift) => unchecked((ulong)((long)x >> shift))
        );
    }

    /// <summary>
    /// 64bitの各laneでゼロ埋め右シフトを行い幅内へ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ShrU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ShrU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerShift(context, 64, static (x, shift) => x >> shift);
    }

    /// <summary>
    /// 単項整数演算のoperandを取り出し結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">laneのビット列へ適用する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdIntegerUnary(
        InterpreterContext context,
        int bitWidth,
        Func<ulong, ulong> operation
    )
    {
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdIntegerUnary(vector, bitWidth, operation).ToValue());
        return default;
    }

    /// <summary>
    /// 二項整数演算のoperandを逆順に取り出し結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">左右のlaneのビット列へ適用する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdIntegerBinary(
        InterpreterContext context,
        int bitWidth,
        Func<ulong, ulong, ulong> operation
    )
    {
        var second = SimdValue.FromValue(context.PopValue());
        var first = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdIntegerBinary(first, second, bitWidth, operation).ToValue());
        return default;
    }

    /// <summary>
    /// i32シフト量をlane幅で剰余化してvectorの演算結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">laneのビット列と剰余化したシフト量へ適用する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdIntegerShift(
        InterpreterContext context,
        int bitWidth,
        Func<ulong, int, ulong> operation
    )
    {
        var shift = (int)(unchecked((uint)context.PopValue().AsI32()) % (uint)bitWidth);
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdIntegerShift(vector, bitWidth, shift, operation).ToValue());
        return default;
    }

    /// <summary>
    /// 単項演算の結果をlane幅へ切り詰め新しいvectorを構築する
    /// </summary>
    /// <param name="vector">入力のビット列</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">laneごとの純粋な整数演算</param>
    /// <returns>演算後のビット列</returns>
    private static SimdValue MapSimdIntegerUnary(
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
    /// 二項演算の結果をlane幅へ切り詰め新しいvectorを構築する
    /// </summary>
    /// <param name="first">左operandのビット列</param>
    /// <param name="second">右operandのビット列</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="operation">左右のlaneへ適用する純粋な整数演算</param>
    /// <returns>演算後のビット列</returns>
    private static SimdValue MapSimdIntegerBinary(
        SimdValue first,
        SimdValue second,
        int bitWidth,
        Func<ulong, ulong, ulong> operation
    )
    {
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            var bits = operation(
                first.GetLane(bitWidth, laneIndex),
                second.GetLane(bitWidth, laneIndex)
            );
            result = result.WithLane(bitWidth, laneIndex, bits);
        }

        return result;
    }

    /// <summary>
    /// シフト演算の結果をlane幅へ切り詰め新しいvectorを構築する
    /// </summary>
    /// <param name="vector">入力のビット列</param>
    /// <param name="bitWidth">laneの幅</param>
    /// <param name="shift">lane幅で剰余化したシフト量</param>
    /// <param name="operation">各laneへ適用する純粋なシフト演算</param>
    /// <returns>演算後のビット列</returns>
    private static SimdValue MapSimdIntegerShift(
        SimdValue vector,
        int bitWidth,
        int shift,
        Func<ulong, int, ulong> operation
    )
    {
        var result = default(SimdValue);
        for (var laneIndex = 0; laneIndex < 128 / bitWidth; laneIndex++)
        {
            result = result.WithLane(
                bitWidth,
                laneIndex,
                operation(vector.GetLane(bitWidth, laneIndex), shift)
            );
        }

        return result;
    }
}
