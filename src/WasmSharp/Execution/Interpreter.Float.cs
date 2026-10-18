namespace WasmSharp.Execution;

internal static partial class Interpreter
{
    /// <summary>
    /// f32の算術結果を生ビットで保持し、NaNを正のcanonical NaNへ揃える
    /// </summary>
    /// <param name="value">対象幅で丸めた算術結果</param>
    /// <returns>算術結果のビット列を持つf32のvalue</returns>
    private static WasmValue F32ArithmeticResult(float value)
    {
        return WasmValue.FromF32Bits(
            float.IsNaN(value) ? 0x7FC00000u : BitConverter.SingleToUInt32Bits(value)
        );
    }

    /// <summary>
    /// f64の算術結果を生ビットで保持し、NaNを正のcanonical NaNへ揃える
    /// </summary>
    /// <param name="value">対象幅で丸めた算術結果</param>
    /// <returns>算術結果のビット列を持つf64のvalue</returns>
    private static WasmValue F64ArithmeticResult(double value)
    {
        return WasmValue.FromF64Bits(
            double.IsNaN(value) ? 0x7FF8000000000000ul : BitConverter.DoubleToUInt64Bits(value)
        );
    }

    /// <summary>
    /// f32の整数値化結果を保持し、0の符号を入力に揃える
    /// </summary>
    /// <param name="value">対象幅の整数値化結果</param>
    /// <param name="operand">整数値化前の入力</param>
    /// <returns>0の符号とNaN規則を適用したf32のvalue</returns>
    private static WasmValue F32RoundedResult(float value, float operand)
    {
        return value == 0
            ? WasmValue.FromF32Bits(BitConverter.SingleToUInt32Bits(operand) & 0x80000000u)
            : F32ArithmeticResult(value);
    }

    /// <summary>
    /// f64の整数値化結果を保持し、0の符号を入力に揃える
    /// </summary>
    /// <param name="value">対象幅の整数値化結果</param>
    /// <param name="operand">整数値化前の入力</param>
    /// <returns>0の符号とNaN規則を適用したf64のvalue</returns>
    private static WasmValue F64RoundedResult(double value, double operand)
    {
        return value == 0
            ? WasmValue.FromF64Bits(BitConverter.DoubleToUInt64Bits(operand) & 0x8000000000000000ul)
            : F64ArithmeticResult(value);
    }

    /// <summary>
    /// NaNと符号付きゼロの規則を適用してf32の最小値を返す
    /// </summary>
    /// <param name="left">左側のoperand</param>
    /// <param name="right">右側のoperand</param>
    /// <returns>最小値のビット列を持つf32のvalue</returns>
    private static WasmValue F32MinResult(float left, float right)
    {
        if (float.IsNaN(left) || float.IsNaN(right))
        {
            return F32ArithmeticResult(float.NaN);
        }
        if (left == right)
        {
            // 等値の非NaNでは符号bitだけが異なり得る ±0はORで−0を選ぶ
            return WasmValue.FromF32Bits(
                BitConverter.SingleToUInt32Bits(left) | BitConverter.SingleToUInt32Bits(right)
            );
        }

        return F32ArithmeticResult(left < right ? left : right);
    }

    /// <summary>
    /// NaNと符号付きゼロの規則を適用してf32の最大値を返す
    /// </summary>
    /// <param name="left">左側のoperand</param>
    /// <param name="right">右側のoperand</param>
    /// <returns>最大値のビット列を持つf32のvalue</returns>
    private static WasmValue F32MaxResult(float left, float right)
    {
        if (float.IsNaN(left) || float.IsNaN(right))
        {
            return F32ArithmeticResult(float.NaN);
        }
        if (left == right)
        {
            // 等値の非NaNでは符号bitだけが異なり得る ±0はANDで+0を選ぶ
            return WasmValue.FromF32Bits(
                BitConverter.SingleToUInt32Bits(left) & BitConverter.SingleToUInt32Bits(right)
            );
        }

        return F32ArithmeticResult(left > right ? left : right);
    }

    /// <summary>
    /// NaNと符号付きゼロの規則を適用してf64の最小値を返す
    /// </summary>
    /// <param name="left">左側のoperand</param>
    /// <param name="right">右側のoperand</param>
    /// <returns>最小値のビット列を持つf64のvalue</returns>
    private static WasmValue F64MinResult(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return F64ArithmeticResult(double.NaN);
        }
        if (left == right)
        {
            // 等値の非NaNでは符号bitだけが異なり得る ±0はORで−0を選ぶ
            return WasmValue.FromF64Bits(
                BitConverter.DoubleToUInt64Bits(left) | BitConverter.DoubleToUInt64Bits(right)
            );
        }

        return F64ArithmeticResult(left < right ? left : right);
    }

    /// <summary>
    /// NaNと符号付きゼロの規則を適用してf64の最大値を返す
    /// </summary>
    /// <param name="left">左側のoperand</param>
    /// <param name="right">右側のoperand</param>
    /// <returns>最大値のビット列を持つf64のvalue</returns>
    private static WasmValue F64MaxResult(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return F64ArithmeticResult(double.NaN);
        }
        if (left == right)
        {
            // 等値の非NaNでは符号bitだけが異なり得る ±0はANDで+0を選ぶ
            return WasmValue.FromF64Bits(
                BitConverter.DoubleToUInt64Bits(left) & BitConverter.DoubleToUInt64Bits(right)
            );
        }

        return F64ArithmeticResult(left > right ? left : right);
    }

    /// <summary>
    /// f32の加算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Add(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        var result = left + right;
        context.PushValue(F32ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f32の減算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Sub(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        var result = left - right;
        context.PushValue(F32ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f32の乗算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Mul(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        var result = left * right;
        context.PushValue(F32ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f32の除算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Div命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Div(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        var result = left / right;
        context.PushValue(F32ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f32の平方根結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Sqrt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Sqrt(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF32();
        var result = MathF.Sqrt(operand);
        context.PushValue(F32ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f64の加算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Add命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Add(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        var result = left + right;
        context.PushValue(F64ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f64の減算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Sub命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Sub(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        var result = left - right;
        context.PushValue(F64ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f64の乗算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Mul命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Mul(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        var result = left * right;
        context.PushValue(F64ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f64の除算結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Div命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Div(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        var result = left / right;
        context.PushValue(F64ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f64の平方根結果を対象幅へ丸める
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Sqrt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Sqrt(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF64();
        var result = Math.Sqrt(operand);
        context.PushValue(F64ArithmeticResult(result));
        return default;
    }

    /// <summary>
    /// f32値を正の無限大方向へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Ceil命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Ceil(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF32();
        var result = MathF.Ceiling(operand);
        context.PushValue(F32RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f32値を負の無限大方向へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Floor命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Floor(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF32();
        var result = MathF.Floor(operand);
        context.PushValue(F32RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f32値を0方向へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Trunc命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Trunc(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF32();
        var result = MathF.Truncate(operand);
        context.PushValue(F32RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f32値を最近接偶数へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Nearest命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Nearest(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF32();
        var result = MathF.Round(operand, MidpointRounding.ToEven);
        context.PushValue(F32RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f32の符号bitを正へ変更する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Abs(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF32Bits();
        context.PushValue(WasmValue.FromF32Bits(operand & 0x7FFFFFFFu));
        return default;
    }

    /// <summary>
    /// f32の符号bitを反転する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Neg(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF32Bits();
        context.PushValue(WasmValue.FromF32Bits(operand ^ 0x80000000u));
        return default;
    }

    /// <summary>
    /// f32の第1operandへ第2operandの符号bitを設定する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Copysign命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Copysign(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var right = context.PopValue().AsF32Bits();
        var left = context.PopValue().AsF32Bits();
        context.PushValue(WasmValue.FromF32Bits((left & 0x7FFFFFFFu) | (right & 0x80000000u)));
        return default;
    }

    /// <summary>
    /// f64値を正の無限大方向へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Ceil命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Ceil(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF64();
        var result = Math.Ceiling(operand);
        context.PushValue(F64RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f64値を負の無限大方向へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Floor命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Floor(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF64();
        var result = Math.Floor(operand);
        context.PushValue(F64RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f64値を0方向へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Trunc命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Trunc(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF64();
        var result = Math.Truncate(operand);
        context.PushValue(F64RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f64値を最近接偶数へ整数値化する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Nearest命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Nearest(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var operand = context.PopValue().AsF64();
        var result = Math.Round(operand, MidpointRounding.ToEven);
        context.PushValue(F64RoundedResult(result, operand));
        return default;
    }

    /// <summary>
    /// f64の符号bitを正へ変更する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Abs命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Abs(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF64Bits();
        context.PushValue(WasmValue.FromF64Bits(operand & 0x7FFFFFFFFFFFFFFFul));
        return default;
    }

    /// <summary>
    /// f64の符号bitを反転する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Neg命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Neg(InterpreterContext context, in Instruction instruction)
    {
        var operand = context.PopValue().AsF64Bits();
        context.PushValue(WasmValue.FromF64Bits(operand ^ 0x8000000000000000ul));
        return default;
    }

    /// <summary>
    /// f64の第1operandへ第2operandの符号bitを設定する
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Copysign命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Copysign(
        InterpreterContext context,
        in Instruction instruction
    )
    {
        var right = context.PopValue().AsF64Bits();
        var left = context.PopValue().AsF64Bits();
        context.PushValue(
            WasmValue.FromF64Bits((left & 0x7FFFFFFFFFFFFFFFul) | (right & 0x8000000000000000ul))
        );
        return default;
    }

    /// <summary>
    /// f32の最小値を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Min命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Min(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(F32MinResult(left, right));
        return default;
    }

    /// <summary>
    /// f32の最大値を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Max命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Max(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(F32MaxResult(left, right));
        return default;
    }

    /// <summary>
    /// f32の等値比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Eq(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(WasmValue.FromI32(left == right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f32の不等値比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Ne(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(WasmValue.FromI32(left != right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f32の小なり比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Lt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Lt(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(WasmValue.FromI32(left < right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f32の大なり比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Gt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Gt(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(WasmValue.FromI32(left > right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f32の以下比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Le命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Le(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(WasmValue.FromI32(left <= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f32の以上比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF32Ge命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F32Ge(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF32();
        var left = context.PopValue().AsF32();
        context.PushValue(WasmValue.FromI32(left >= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f64の最小値を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Min命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Min(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(F64MinResult(left, right));
        return default;
    }

    /// <summary>
    /// f64の最大値を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Max命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Max(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(F64MaxResult(left, right));
        return default;
    }

    /// <summary>
    /// f64の等値比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Eq命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Eq(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(WasmValue.FromI32(left == right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f64の不等値比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Ne命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Ne(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(WasmValue.FromI32(left != right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f64の小なり比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Lt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Lt(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(WasmValue.FromI32(left < right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f64の大なり比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Gt命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Gt(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(WasmValue.FromI32(left > right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f64の以下比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Le命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Le(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(WasmValue.FromI32(left <= right ? 1 : 0));
        return default;
    }

    /// <summary>
    /// f64の以上比較を返す
    /// </summary>
    /// <param name="context">検証済みのoperandを持つ実行コンテキスト</param>
    /// <param name="instruction">検証済みのF64Ge命令</param>
    /// <returns>実行の成功</returns>
    internal static ExecutionResult F64Ge(InterpreterContext context, in Instruction instruction)
    {
        var right = context.PopValue().AsF64();
        var left = context.PopValue().AsF64();
        context.PushValue(WasmValue.FromI32(left >= right ? 1 : 0));
        return default;
    }
}
