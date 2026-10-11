namespace WasmSharp.Execution;

internal static partial class Interpreter
{
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
