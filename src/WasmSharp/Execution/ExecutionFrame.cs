namespace WasmSharp.Execution;

/// <summary>
/// 実行中の関数、関数内の命令位置と値スタックの基準
/// </summary>
/// <remarks>関数と値スタックの基準を保持し、命令位置を0に初期化する</remarks>
/// <param name="function">実行する関数</param>
/// <param name="stackBase">共有値スタック上の引数とlocalsの開始位置</param>
/// <param name="operandBase">共有値スタック上のoperand領域の開始位置</param>
internal struct ExecutionFrame(WasmFunction function, int stackBase, int operandBase)
{
    /// <summary>
    /// このフレームで実行する関数
    /// </summary>
    internal WasmFunction Function { get; } = function;

    /// <summary>
    /// 関数内で次に実行する命令の位置
    /// </summary>
    internal int Pc { get; set; }

    /// <summary>
    /// 共有値スタック上で引数とlocalsが始まる位置
    /// </summary>
    internal int StackBase { get; } = stackBase;

    /// <summary>
    /// 共有値スタック上で引数とlocalsの後に続くoperand領域の開始位置
    /// </summary>
    internal int OperandBase { get; } = operandBase;
}
