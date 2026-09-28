namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 入力・command・6分類の保存用集計
/// </summary>
/// <param name="InputCount">本来の全対象入力数</param>
/// <param name="ProcessedInputCount">列挙済みの全commandを記録した入力数</param>
/// <param name="IncompleteInputCount">処理を開始したが中断した入力数</param>
/// <param name="UnprocessedInputCount">処理を開始していない入力数</param>
/// <param name="UndeterminedInputCount">処理を開始し、command件数が未確定の入力数</param>
/// <param name="IssueInputCount">入力異常を持つ入力数</param>
/// <param name="InputIssueCount">入力異常の診断数</param>
/// <param name="EnumeratedCommandCount">境界を確定できたcommand数</param>
/// <param name="UnprocessedCommandCount">列挙済みのうち処理していないcommand数</param>
/// <param name="Setup">セットアップの6分類</param>
/// <param name="Action">単独actionの6分類</param>
/// <param name="Assertion">assertionの6分類</param>
/// <param name="UncategorizedRunnerErrorCount">種類を確定できないcommandのrunner_error数</param>
internal sealed record RunSummary(
    int InputCount,
    int ProcessedInputCount,
    int IncompleteInputCount,
    int UnprocessedInputCount,
    int UndeterminedInputCount,
    int IssueInputCount,
    int InputIssueCount,
    int EnumeratedCommandCount,
    int UnprocessedCommandCount,
    OutcomeCounts Setup,
    OutcomeCounts Action,
    OutcomeCounts Assertion,
    int UncategorizedRunnerErrorCount
)
{
    /// <summary>
    /// 全件数が0の集計
    /// </summary>
    internal static RunSummary Empty { get; } =
        new(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            OutcomeCounts.Count([]),
            OutcomeCounts.Count([]),
            OutcomeCounts.Count([]),
            0
        );
}
