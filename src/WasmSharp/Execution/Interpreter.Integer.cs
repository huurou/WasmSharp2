namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// i32の加算を対象幅の剰余として計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Add(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(unchecked(left + right)));
        return default;
    }

    /// <summary>
    /// i32の減算を対象幅の剰余として計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Sub(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(unchecked(left - right)));
        return default;
    }

    /// <summary>
    /// i32の乗算を対象幅の剰余として計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Mul(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(unchecked(left * right)));
        return default;
    }

    /// <summary>
    /// i64の加算を対象幅の剰余として計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Add(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI64(unchecked(left + right)));
        return default;
    }

    /// <summary>
    /// i64の減算を対象幅の剰余として計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Sub(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI64(unchecked(left - right)));
        return default;
    }

    /// <summary>
    /// i64の乗算を対象幅の剰余として計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Mul(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI64(unchecked(left * right)));
        return default;
    }

    /// <summary>
    /// i32のゼロ判定をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Eqz命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Eqz(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(operand == 0 ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の等値比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Eq(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(left == right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の非等値比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32Ne(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(left != right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32LtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32LtS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(left < right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32LtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32LtU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((uint)context.PopValue().AsI32());
        var left = unchecked((uint)context.PopValue().AsI32());
        context.PushValue(WasmValue.FromI32(left < right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32GtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32GtS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(left > right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32GtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32GtU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((uint)context.PopValue().AsI32());
        var left = unchecked((uint)context.PopValue().AsI32());
        context.PushValue(WasmValue.FromI32(left > right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32LeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32LeS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(left <= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32LeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32LeU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((uint)context.PopValue().AsI32());
        var left = unchecked((uint)context.PopValue().AsI32());
        context.PushValue(WasmValue.FromI32(left <= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32GeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32GeS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        context.PushValue(WasmValue.FromI32(left >= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i32の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32GeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I32GeU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((uint)context.PopValue().AsI32());
        var left = unchecked((uint)context.PopValue().AsI32());
        context.PushValue(WasmValue.FromI32(left >= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64のゼロ判定をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Eqz命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Eqz(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(operand == 0 ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の等値比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Eq(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(left == right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の非等値比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64Ne(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(left != right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64LtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64LtS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(left < right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64LtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64LtU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((ulong)context.PopValue().AsI64());
        var left = unchecked((ulong)context.PopValue().AsI64());
        context.PushValue(WasmValue.FromI32(left < right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64GtS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64GtS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(left > right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64GtU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64GtU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((ulong)context.PopValue().AsI64());
        var left = unchecked((ulong)context.PopValue().AsI64());
        context.PushValue(WasmValue.FromI32(left > right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64LeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64LeS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(left <= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64LeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64LeU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((ulong)context.PopValue().AsI64());
        var left = unchecked((ulong)context.PopValue().AsI64());
        context.PushValue(WasmValue.FromI32(left <= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号付き大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64GeS命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64GeS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        context.PushValue(WasmValue.FromI32(left >= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// i64の符号なし大小比較をi32の0または1で返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64GeU命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult I64GeU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((ulong)context.PopValue().AsI64());
        var left = unchecked((ulong)context.PopValue().AsI64());
        context.PushValue(WasmValue.FromI32(left >= right ? 1 : 0));
        return default;
    }
}
