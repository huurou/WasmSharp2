namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 操作の終了状態。値はプロセスの終了値と一致する
/// </summary>
internal enum CompletionStatus
{
    /// <summary>
    /// 操作が完了し、用途の合格条件を満たした状態
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// 操作は完了して報告できたが、用途の合格条件を満たさない状態
    /// </summary>
    Unsatisfied = 1,

    /// <summary>
    /// 読取・中断・保存失敗・比較未成立などにより操作を完了できない状態
    /// </summary>
    Incomplete = 2,
}
