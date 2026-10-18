using System.Numerics;

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
    /// 8bitの各laneで1のbit数を数える
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16Popcnt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16Popcnt(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerUnary(context, 8, static x => (ulong)BitOperations.PopCount(x));
    }

    /// <summary>
    /// 16bitのsigned入力を8bitの符号付き範囲へ飽和して連結する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16NarrowI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16NarrowI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerNarrow(
            context,
            16,
            static x => SaturateSimdInteger(unchecked((short)x), sbyte.MinValue, sbyte.MaxValue)
        );
    }

    /// <summary>
    /// 16bitのsigned入力を8bitの符号なし範囲へ飽和して連結する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16NarrowI16x8U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16NarrowI16x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerNarrow(
            context,
            16,
            static x => SaturateSimdInteger(unchecked((short)x), 0, byte.MaxValue)
        );
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
    /// 8bitの各laneで符号付き加算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16AddSatS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16AddSatS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            8,
            static (x, y) =>
                SaturateSimdInteger(unchecked((sbyte)x + (sbyte)y), sbyte.MinValue, sbyte.MaxValue)
        );
    }

    /// <summary>
    /// 8bitの各laneで符号なし加算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16AddSatU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16AddSatU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            8,
            static (x, y) => SaturateSimdInteger((int)x + (int)y, 0, byte.MaxValue)
        );
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
    /// 8bitの各laneで符号付き減算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16SubSatS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16SubSatS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            8,
            static (x, y) =>
                SaturateSimdInteger(unchecked((sbyte)x - (sbyte)y), sbyte.MinValue, sbyte.MaxValue)
        );
    }

    /// <summary>
    /// 8bitの各laneで符号なし減算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16SubSatU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16SubSatU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            8,
            static (x, y) => SaturateSimdInteger((int)x - (int)y, 0, byte.MaxValue)
        );
    }

    /// <summary>
    /// 8bitの各laneを符号付きで比較して最小値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16MinS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16MinS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            8,
            static (x, y) => unchecked((sbyte)x) <= unchecked((sbyte)y) ? x : y
        );
    }

    /// <summary>
    /// 8bitの各laneを符号なしで比較して最小値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16MinU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16MinU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 8, static (x, y) => x <= y ? x : y);
    }

    /// <summary>
    /// 8bitの各laneを符号付きで比較して最大値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16MaxS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16MaxS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            8,
            static (x, y) => unchecked((sbyte)x) >= unchecked((sbyte)y) ? x : y
        );
    }

    /// <summary>
    /// 8bitの各laneを符号なしで比較して最大値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16MaxU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16MaxU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 8, static (x, y) => x >= y ? x : y);
    }

    /// <summary>
    /// 8bitの各laneで符号なしの和を広い中間値に保持して平均を切り上げる
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI8x16AvgrU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I8x16AvgrU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 8, static (x, y) => (x + y + 1) >> 1);
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
    /// 32bitのsigned入力を16bitの符号付き範囲へ飽和して連結する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8NarrowI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8NarrowI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerNarrow(
            context,
            32,
            static x => SaturateSimdInteger(unchecked((int)x), short.MinValue, short.MaxValue)
        );
    }

    /// <summary>
    /// 32bitのsigned入力を16bitの符号なし範囲へ飽和して連結する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8NarrowI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8NarrowI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerNarrow(
            context,
            32,
            static x => SaturateSimdInteger(unchecked((int)x), 0, ushort.MaxValue)
        );
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
    /// 16bitの各laneで符号付き加算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8AddSatS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8AddSatS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            16,
            static (x, y) =>
                SaturateSimdInteger(unchecked((short)x + (short)y), short.MinValue, short.MaxValue)
        );
    }

    /// <summary>
    /// 16bitの各laneで符号なし加算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8AddSatU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8AddSatU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            16,
            static (x, y) => SaturateSimdInteger((int)x + (int)y, 0, ushort.MaxValue)
        );
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
    /// 16bitの各laneで符号付き減算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8SubSatS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8SubSatS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            16,
            static (x, y) =>
                SaturateSimdInteger(unchecked((short)x - (short)y), short.MinValue, short.MaxValue)
        );
    }

    /// <summary>
    /// 16bitの各laneで符号なし減算を行い出力範囲へ飽和する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8SubSatU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8SubSatU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            16,
            static (x, y) => SaturateSimdInteger((int)x - (int)y, 0, ushort.MaxValue)
        );
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
    /// 16bitの各laneを符号付きで比較して最小値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8MinS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8MinS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            16,
            static (x, y) => unchecked((short)x) <= unchecked((short)y) ? x : y
        );
    }

    /// <summary>
    /// 16bitの各laneを符号なしで比較して最小値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8MinU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8MinU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 16, static (x, y) => x <= y ? x : y);
    }

    /// <summary>
    /// 16bitの各laneを符号付きで比較して最大値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8MaxS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8MaxS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            16,
            static (x, y) => unchecked((short)x) >= unchecked((short)y) ? x : y
        );
    }

    /// <summary>
    /// 16bitの各laneを符号なしで比較して最大値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8MaxU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8MaxU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 16, static (x, y) => x >= y ? x : y);
    }

    /// <summary>
    /// 16bitの各laneで符号なしの和を広い中間値に保持して平均を切り上げる
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8AvgrU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8AvgrU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 16, static (x, y) => (x + y + 1) >> 1);
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
    /// 32bitの各laneを符号付きで比較して最小値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4MinS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4MinS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            32,
            static (x, y) => unchecked((int)x) <= unchecked((int)y) ? x : y
        );
    }

    /// <summary>
    /// 32bitの各laneを符号なしで比較して最小値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4MinU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4MinU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 32, static (x, y) => x <= y ? x : y);
    }

    /// <summary>
    /// 32bitの各laneを符号付きで比較して最大値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4MaxS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4MaxS(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(
            context,
            32,
            static (x, y) => unchecked((int)x) >= unchecked((int)y) ? x : y
        );
    }

    /// <summary>
    /// 32bitの各laneを符号なしで比較して最大値のビット列を選ぶ
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4MaxU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4MaxU(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerBinary(context, 32, static (x, y) => x >= y ? x : y);
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
    /// 8bitの下側laneを符号付きで16bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtendLowI8x16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtendLowI8x16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(
            context,
            8,
            high: false,
            static x => unchecked((ulong)(sbyte)x)
        );
    }

    /// <summary>
    /// 8bitの下側laneを符号なしで16bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtendLowI8x16U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtendLowI8x16U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(context, 8, high: false, static x => x);
    }

    /// <summary>
    /// 8bitの上側laneを符号付きで16bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtendHighI8x16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtendHighI8x16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(
            context,
            8,
            high: true,
            static x => unchecked((ulong)(sbyte)x)
        );
    }

    /// <summary>
    /// 8bitの上側laneを符号なしで16bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtendHighI8x16U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtendHighI8x16U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(context, 8, high: true, static x => x);
    }

    /// <summary>
    /// 8bitの下側laneを符号付きで16bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtMulLowI8x16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtMulLowI8x16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(
            context,
            8,
            high: false,
            static (x, y) => unchecked((ulong)((long)(sbyte)x * (sbyte)y))
        );
    }

    /// <summary>
    /// 8bitの下側laneを符号なしで16bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtMulLowI8x16U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtMulLowI8x16U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(context, 8, high: false, static (x, y) => x * y);
    }

    /// <summary>
    /// 8bitの上側laneを符号付きで16bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtMulHighI8x16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtMulHighI8x16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(
            context,
            8,
            high: true,
            static (x, y) => unchecked((ulong)((long)(sbyte)x * (sbyte)y))
        );
    }

    /// <summary>
    /// 8bitの上側laneを符号なしで16bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtMulHighI8x16U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtMulHighI8x16U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(context, 8, high: true, static (x, y) => x * y);
    }

    /// <summary>
    /// 16bitの下側laneを符号付きで32bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtendLowI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtendLowI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(
            context,
            16,
            high: false,
            static x => unchecked((ulong)(short)x)
        );
    }

    /// <summary>
    /// 16bitの下側laneを符号なしで32bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtendLowI16x8U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtendLowI16x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(context, 16, high: false, static x => x);
    }

    /// <summary>
    /// 16bitの上側laneを符号付きで32bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtendHighI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtendHighI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(
            context,
            16,
            high: true,
            static x => unchecked((ulong)(short)x)
        );
    }

    /// <summary>
    /// 16bitの上側laneを符号なしで32bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtendHighI16x8U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtendHighI16x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(context, 16, high: true, static x => x);
    }

    /// <summary>
    /// 16bitの下側laneを符号付きで32bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtMulLowI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtMulLowI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(
            context,
            16,
            high: false,
            static (x, y) => unchecked((ulong)((long)(short)x * (short)y))
        );
    }

    /// <summary>
    /// 16bitの下側laneを符号なしで32bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtMulLowI16x8U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtMulLowI16x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(context, 16, high: false, static (x, y) => x * y);
    }

    /// <summary>
    /// 16bitの上側laneを符号付きで32bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtMulHighI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtMulHighI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(
            context,
            16,
            high: true,
            static (x, y) => unchecked((ulong)((long)(short)x * (short)y))
        );
    }

    /// <summary>
    /// 16bitの上側laneを符号なしで32bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtMulHighI16x8U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtMulHighI16x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(context, 16, high: true, static (x, y) => x * y);
    }

    /// <summary>
    /// 32bitの下側laneを符号付きで64bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtendLowI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtendLowI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(
            context,
            32,
            high: false,
            static x => unchecked((ulong)(int)x)
        );
    }

    /// <summary>
    /// 32bitの下側laneを符号なしで64bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtendLowI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtendLowI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(context, 32, high: false, static x => x);
    }

    /// <summary>
    /// 32bitの上側laneを符号付きで64bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtendHighI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtendHighI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(
            context,
            32,
            high: true,
            static x => unchecked((ulong)(int)x)
        );
    }

    /// <summary>
    /// 32bitの上側laneを符号なしで64bitへ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtendHighI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtendHighI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtend(context, 32, high: true, static x => x);
    }

    /// <summary>
    /// 32bitの下側laneを符号付きで64bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtMulLowI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtMulLowI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(
            context,
            32,
            high: false,
            static (x, y) => unchecked((ulong)((long)(int)x * (int)y))
        );
    }

    /// <summary>
    /// 32bitの下側laneを符号なしで64bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtMulLowI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtMulLowI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(context, 32, high: false, static (x, y) => x * y);
    }

    /// <summary>
    /// 32bitの上側laneを符号付きで64bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtMulHighI32x4S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtMulHighI32x4S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(
            context,
            32,
            high: true,
            static (x, y) => unchecked((ulong)((long)(int)x * (int)y))
        );
    }

    /// <summary>
    /// 32bitの上側laneを符号なしで64bitへ拡張して乗算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64x2ExtMulHighI32x4U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64x2ExtMulHighI32x4U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerExtMul(context, 32, high: true, static (x, y) => x * y);
    }

    /// <summary>
    /// 隣接する8bitのsigned laneを16bitへ拡張して加算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtAddPairwiseI8x16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtAddPairwiseI8x16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerUnary(
            context,
            16,
            static x => unchecked((ulong)((sbyte)x + (sbyte)(x >> 8)))
        );
    }

    /// <summary>
    /// 隣接する8bitのunsigned laneを16bitへ拡張して加算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI16x8ExtAddPairwiseI8x16U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I16x8ExtAddPairwiseI8x16U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerUnary(context, 16, static x => (x & 0xFFUL) + (x >> 8));
    }

    /// <summary>
    /// 隣接する16bitのsigned laneを32bitへ拡張して加算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtAddPairwiseI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtAddPairwiseI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerUnary(
            context,
            32,
            static x => unchecked((ulong)((short)x + (short)(x >> 16)))
        );
    }

    /// <summary>
    /// 隣接する16bitのunsigned laneを32bitへ拡張して加算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4ExtAddPairwiseI16x8U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4ExtAddPairwiseI16x8U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecuteSimdIntegerUnary(context, 32, static x => (x & 0xFFFFUL) + (x >> 16));
    }

    /// <summary>
    /// 隣接するsigned i16の2積を加算して32bitへ折り返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32x4DotI16x8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32x4DotI16x8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        // 2つの最小i16の積和は2147483648になるためlongで加算してから32bitへ折り返す
        return ExecuteSimdIntegerBinary(
            context,
            32,
            static (x, y) =>
                unchecked(
                    (ulong)((long)(short)x * (short)y + (long)(short)(x >> 16) * (short)(y >> 16))
                )
        );
    }

    /// <summary>
    /// extendのoperandを取り出し選択した半分のlaneを拡張して積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="sourceBitWidth">入力laneの幅</param>
    /// <param name="high">上側の半分を選択するか</param>
    /// <param name="extend">符号解釈に従ってlaneを拡張する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdIntegerExtend(
        InterpreterContext context,
        int sourceBitWidth,
        bool high,
        Func<ulong, ulong> extend
    )
    {
        var vector = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdIntegerExtend(vector, sourceBitWidth, high, extend).ToValue());
        return default;
    }

    /// <summary>
    /// extmulのoperandを逆順に取り出し選択した半分の拡張乗算結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="sourceBitWidth">入力laneの幅</param>
    /// <param name="high">上側の半分を選択するか</param>
    /// <param name="multiply">符号解釈に従って拡張幅で乗算する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdIntegerExtMul(
        InterpreterContext context,
        int sourceBitWidth,
        bool high,
        Func<ulong, ulong, ulong> multiply
    )
    {
        var second = SimdValue.FromValue(context.PopValue());
        var first = SimdValue.FromValue(context.PopValue());
        context.PushValue(
            MapSimdIntegerExtMul(first, second, sourceBitWidth, high, multiply).ToValue()
        );
        return default;
    }

    /// <summary>
    /// 入力の半分を選択して出力lane 0から拡張したビット列を並べる
    /// </summary>
    /// <param name="vector">入力のビット列</param>
    /// <param name="sourceBitWidth">入力laneの幅</param>
    /// <param name="high">上側の半分を選択するか</param>
    /// <param name="extend">laneごとの純粋な拡張演算</param>
    /// <returns>倍のlane幅へ拡張したビット列</returns>
    private static SimdValue MapSimdIntegerExtend(
        SimdValue vector,
        int sourceBitWidth,
        bool high,
        Func<ulong, ulong> extend
    )
    {
        var result = default(SimdValue);
        var destinationBitWidth = sourceBitWidth * 2;
        var laneCount = 128 / destinationBitWidth;
        var firstLaneIndex = high ? laneCount : 0;
        for (var laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            result = result.WithLane(
                destinationBitWidth,
                laneIndex,
                extend(vector.GetLane(sourceBitWidth, firstLaneIndex + laneIndex))
            );
        }

        return result;
    }

    /// <summary>
    /// 両operandの同じ半分を選択し拡張幅の積を出力lane 0から並べる
    /// </summary>
    /// <param name="first">左operandのビット列</param>
    /// <param name="second">右operandのビット列</param>
    /// <param name="sourceBitWidth">入力laneの幅</param>
    /// <param name="high">上側の半分を選択するか</param>
    /// <param name="multiply">laneごとの純粋な拡張乗算</param>
    /// <returns>倍のlane幅に保持した積のビット列</returns>
    private static SimdValue MapSimdIntegerExtMul(
        SimdValue first,
        SimdValue second,
        int sourceBitWidth,
        bool high,
        Func<ulong, ulong, ulong> multiply
    )
    {
        var result = default(SimdValue);
        var destinationBitWidth = sourceBitWidth * 2;
        var laneCount = 128 / destinationBitWidth;
        var firstLaneIndex = high ? laneCount : 0;
        for (var laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            var sourceLaneIndex = firstLaneIndex + laneIndex;
            result = result.WithLane(
                destinationBitWidth,
                laneIndex,
                multiply(
                    first.GetLane(sourceBitWidth, sourceLaneIndex),
                    second.GetLane(sourceBitWidth, sourceLaneIndex)
                )
            );
        }

        return result;
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
    /// narrowのoperandを逆順に取り出し幅を縮めた結果を積む
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="sourceBitWidth">入力laneの幅</param>
    /// <param name="saturate">signed入力を出力範囲へ飽和する演算</param>
    /// <returns>実行の成功</returns>
    private static ExecutionResult ExecuteSimdIntegerNarrow(
        InterpreterContext context,
        int sourceBitWidth,
        Func<ulong, ulong> saturate
    )
    {
        var second = SimdValue.FromValue(context.PopValue());
        var first = SimdValue.FromValue(context.PopValue());
        context.PushValue(MapSimdIntegerNarrow(first, second, sourceBitWidth, saturate).ToValue());
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
    /// 広い整数の中間値を出力範囲へ制限してlane用のビット列を返す
    /// </summary>
    /// <param name="value">幅内へ折り返す前の整数値</param>
    /// <param name="minimum">出力範囲の下限</param>
    /// <param name="maximum">出力範囲の上限</param>
    /// <returns>飽和した整数のビット列</returns>
    private static ulong SaturateSimdInteger(int value, int minimum, int maximum)
    {
        return unchecked((ulong)Math.Clamp(value, minimum, maximum));
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
    /// 両operandのlaneを飽和して第1operandから順に半分の幅へ連結する
    /// </summary>
    /// <param name="first">下側へ配置する左operandのビット列</param>
    /// <param name="second">上側へ配置する右operandのビット列</param>
    /// <param name="sourceBitWidth">入力laneの幅</param>
    /// <param name="saturate">signed入力を出力範囲へ飽和する純粋演算</param>
    /// <returns>出力幅へ飽和して連結したビット列</returns>
    private static SimdValue MapSimdIntegerNarrow(
        SimdValue first,
        SimdValue second,
        int sourceBitWidth,
        Func<ulong, ulong> saturate
    )
    {
        var result = default(SimdValue);
        var destinationBitWidth = sourceBitWidth / 2;
        var sourceLaneCount = 128 / sourceBitWidth;
        for (var laneIndex = 0; laneIndex < sourceLaneCount; laneIndex++)
        {
            result = result.WithLane(
                destinationBitWidth,
                laneIndex,
                saturate(first.GetLane(sourceBitWidth, laneIndex))
            );
            result = result.WithLane(
                destinationBitWidth,
                laneIndex + sourceLaneCount,
                saturate(second.GetLane(sourceBitWidth, laneIndex))
            );
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
