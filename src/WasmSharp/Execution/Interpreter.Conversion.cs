using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// f32を0方向へ切り捨てて符号付きi32へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32TruncF32S命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I32TruncF32S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF32();
        return TruncateI32(operand, true, context, in instruction);
    }

    /// <summary>
    /// f32を0方向へ切り捨てて符号なしi32へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32TruncF32U命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I32TruncF32U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF32();
        return TruncateI32(operand, false, context, in instruction);
    }

    /// <summary>
    /// f64を0方向へ切り捨てて符号付きi32へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32TruncF64S命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I32TruncF64S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF64();
        return TruncateI32(operand, true, context, in instruction);
    }

    /// <summary>
    /// f64を0方向へ切り捨てて符号なしi32へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32TruncF64U命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I32TruncF64U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF64();
        return TruncateI32(operand, false, context, in instruction);
    }

    /// <summary>
    /// f32を0方向へ切り捨てて符号付きi64へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64TruncF32S命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I64TruncF32S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF32();
        return TruncateI64(operand, true, context, in instruction);
    }

    /// <summary>
    /// f32を0方向へ切り捨てて符号なしi64へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64TruncF32U命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I64TruncF32U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF32();
        return TruncateI64(operand, false, context, in instruction);
    }

    /// <summary>
    /// f64を0方向へ切り捨てて符号付きi64へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64TruncF64S命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I64TruncF64S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF64();
        return TruncateI64(operand, true, context, in instruction);
    }

    /// <summary>
    /// f64を0方向へ切り捨てて符号なしi64へ変換する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64TruncF64U命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    internal static ExecutionResult I64TruncF64U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF64();
        return TruncateI64(operand, false, context, in instruction);
    }

    /// <summary>
    /// 浮動小数点値を0方向へ切り捨ててi32へ変換する
    /// </summary>
    /// <param name="operand">doubleで正確に表した変換元の値</param>
    /// <param name="signed">符号付き整数として変換する場合はtrue</param>
    /// <param name="context">結果を積む実行コンテキスト</param>
    /// <param name="instruction">trapの位置を持つ検証済みの変換命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    private static ExecutionResult TruncateI32(
        double operand,
        bool signed,
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var result = TruncateInteger(
            operand,
            signed ? -2147483648D : 0D,
            signed ? 2147483648D : 4294967296D,
            context,
            in instruction,
            out var truncated
        );
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        context.PushValue(
            WasmValue.FromI32(signed ? (int)truncated : unchecked((int)(uint)truncated))
        );
        return default;
    }

    /// <summary>
    /// 浮動小数点値を0方向へ切り捨ててi64へ変換する
    /// </summary>
    /// <param name="operand">doubleで正確に表した変換元の値</param>
    /// <param name="signed">符号付き整数として変換する場合はtrue</param>
    /// <param name="context">結果を積む実行コンテキスト</param>
    /// <param name="instruction">trapの位置を持つ検証済みの変換命令</param>
    /// <returns>実行の成功または整数変換のtrap</returns>
    private static ExecutionResult TruncateI64(
        double operand,
        bool signed,
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var result = TruncateInteger(
            operand,
            signed ? -9223372036854775808D : 0D,
            signed ? 9223372036854775808D : 18446744073709551616D,
            context,
            in instruction,
            out var truncated
        );
        if (result.Status != ExecutionStatus.Success)
        {
            return result;
        }

        context.PushValue(
            WasmValue.FromI64(signed ? (long)truncated : unchecked((long)(ulong)truncated))
        );
        return default;
    }

    /// <summary>
    /// NaNを区別し、0方向への切捨て結果を半開区間で判定する
    /// </summary>
    /// <param name="operand">doubleで正確に表した変換元の値</param>
    /// <param name="lowerBound">cast可能な区間に含む下限</param>
    /// <param name="upperBound">cast可能な区間に含まない正確な2冪の上限</param>
    /// <param name="context">現在関数を保持する実行コンテキスト</param>
    /// <param name="instruction">trapの位置を持つ検証済みの変換命令</param>
    /// <param name="truncated">成功時にcast可能な切捨て結果</param>
    /// <returns>区間内の成功、または原因を区別した整数変換のtrap</returns>
    private static ExecutionResult TruncateInteger(
        double operand,
        double lowerBound,
        double upperBound,
        InterpreterContext context,
        in Instruction instruction,
        out double truncated
    )
    {
        truncated = 0;
        if (double.IsNaN(operand))
        {
            return NumericTrap(WasmTrapReason.InvalidConversionToInteger, context, in instruction);
        }

        // 入力ではなく切捨て後に判定し、下限付近の小数とunsignedの負の小数を許容する。
        truncated = Math.Truncate(operand);
        if (truncated < lowerBound || truncated >= upperBound)
        {
            return NumericTrap(WasmTrapReason.IntegerOverflow, context, in instruction);
        }

        return default;
    }

    /// <summary>
    /// i64の下位32bitをi32として返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32WrapI64命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32WrapI64(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(unchecked((int)operand)));
        return default;
    }

    /// <summary>
    /// i32の符号をi64へ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64ExtendI32S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64ExtendI32S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI64(operand));
        return default;
    }

    /// <summary>
    /// i32を符号なしで解釈してi64へゼロ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64ExtendI32U命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64ExtendI32U(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI64(unchecked((uint)operand)));
        return default;
    }

    /// <summary>
    /// i32の下位8bitを符号付きとしてi32へ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Extend8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Extend8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(unchecked((sbyte)operand)));
        return default;
    }

    /// <summary>
    /// i32の下位16bitを符号付きとしてi32へ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Extend16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Extend16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(unchecked((short)operand)));
        return default;
    }

    /// <summary>
    /// i64の下位8bitを符号付きとしてi64へ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Extend8S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Extend8S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI64(unchecked((sbyte)operand)));
        return default;
    }

    /// <summary>
    /// i64の下位16bitを符号付きとしてi64へ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Extend16S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Extend16S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI64(unchecked((short)operand)));
        return default;
    }

    /// <summary>
    /// i64の下位32bitを符号付きとしてi64へ拡張する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Extend32S命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Extend32S(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI64(unchecked((int)operand)));
        return default;
    }

    /// <summary>
    /// f32の全bitを保持してi32へ再解釈する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32ReinterpretF32命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32ReinterpretF32(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF32Bits();
        context.PushValue(WasmValue.FromI32(unchecked((int)operand)));
        return default;
    }

    /// <summary>
    /// f64の全bitを保持してi64へ再解釈する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64ReinterpretF64命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64ReinterpretF64(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF64Bits();
        context.PushValue(WasmValue.FromI64(unchecked((long)operand)));
        return default;
    }

    /// <summary>
    /// i32の全bitを保持してf32へ再解釈する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32ReinterpretI32命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32ReinterpretI32(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromF32Bits(unchecked((uint)operand)));
        return default;
    }

    /// <summary>
    /// i64の全bitを保持してf64へ再解釈する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64ReinterpretI64命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64ReinterpretI64(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromF64Bits(unchecked((ulong)operand)));
        return default;
    }
}
