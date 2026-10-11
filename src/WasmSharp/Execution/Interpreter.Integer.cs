using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// 整数演算のtrapを現在関数と命令位置を保持して返す
    /// </summary>
    /// <param name="reason">整数演算のtrapの原因</param>
    /// <param name="context">現在関数を保持する実行コンテキスト</param>
    /// <param name="instruction">trapが発生する整数命令</param>
    /// <returns>原因と発生位置を持つtrap</returns>
    private static ExecutionResult IntegerTrap(
        WasmTrapReason reason,
        InterpreterContext context,
        in Instruction instruction
    )
    {
        return ExecutionResult.Trap(
            reason,
            context.CurrentFunction.FunctionIndex,
            instruction.ByteOffset
        );
    }

    /// <summary>
    /// i32の符号付き整数商を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32DivS命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I32DivS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        if (left == int.MinValue && right == -1)
        {
            return IntegerTrap(WasmTrapReason.IntegerOverflow, context, in instruction);
        }

        context.PushValue(WasmValue.FromI32(left / right));
        return default;
    }

    /// <summary>
    /// i32の符号なし整数商を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32DivU命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I32DivU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((uint)context.PopValue().AsI32());
        var left = unchecked((uint)context.PopValue().AsI32());
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        context.PushValue(WasmValue.FromI32(unchecked((int)(left / right))));
        return default;
    }

    /// <summary>
    /// i32の符号付き剰余を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32RemS命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I32RemS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI32();
        var left = context.PopValue().AsI32();
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        // CLRの剰余演算もMIN/-1で例外になるため先に0へ揃える
        var remainder = left == int.MinValue && right == -1 ? 0 : left % right;
        context.PushValue(WasmValue.FromI32(remainder));
        return default;
    }

    /// <summary>
    /// i32の符号なし剰余を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI32RemU命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I32RemU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((uint)context.PopValue().AsI32());
        var left = unchecked((uint)context.PopValue().AsI32());
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        context.PushValue(WasmValue.FromI32(unchecked((int)(left % right))));
        return default;
    }

    /// <summary>
    /// i64の符号付き整数商を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64DivS命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I64DivS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        if (left == long.MinValue && right == -1)
        {
            return IntegerTrap(WasmTrapReason.IntegerOverflow, context, in instruction);
        }

        context.PushValue(WasmValue.FromI64(left / right));
        return default;
    }

    /// <summary>
    /// i64の符号なし整数商を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64DivU命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I64DivU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((ulong)context.PopValue().AsI64());
        var left = unchecked((ulong)context.PopValue().AsI64());
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        context.PushValue(WasmValue.FromI64(unchecked((long)(left / right))));
        return default;
    }

    /// <summary>
    /// i64の符号付き剰余を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64RemS命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I64RemS(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsI64();
        var left = context.PopValue().AsI64();
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        // CLRの剰余演算もMIN/-1で例外になるため先に0へ揃える
        var remainder = left == long.MinValue && right == -1 ? 0 : left % right;
        context.PushValue(WasmValue.FromI64(remainder));
        return default;
    }

    /// <summary>
    /// i64の符号なし剰余を計算する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのI64RemU命令</param>
    /// <returns>実行の成功または整数演算のtrap</returns>
    internal static ExecutionResult I64RemU(InterpreterContext context, in Instruction instruction)
    {
        var right = unchecked((ulong)context.PopValue().AsI64());
        var left = unchecked((ulong)context.PopValue().AsI64());
        if (right == 0)
        {
            return IntegerTrap(WasmTrapReason.IntegerDivideByZero, context, in instruction);
        }

        context.PushValue(WasmValue.FromI64(unchecked((long)(left % right))));
        return default;
    }

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
