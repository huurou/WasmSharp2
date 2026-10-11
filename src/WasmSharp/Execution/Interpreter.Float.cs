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
}
