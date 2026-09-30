namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 一つの区分に属するケースの6分類別件数
/// </summary>
/// <param name="Passed">passedの件数</param>
/// <param name="Failed">failedの件数</param>
/// <param name="RuntimeUnsupported">runtime_unsupportedの件数</param>
/// <param name="RunnerError">runner_errorの件数</param>
/// <param name="OutOfScope">out_of_scopeの件数</param>
/// <param name="Blocked">blockedの件数</param>
internal sealed record OutcomeCounts(
    int Passed,
    int Failed,
    int RuntimeUnsupported,
    int RunnerError,
    int OutOfScope,
    int Blocked
)
{
    /// <summary>
    /// ケースを6分類ごとに数える。
    /// </summary>
    /// <param name="cases">同じ区分に属するケース結果の一覧</param>
    /// <returns>passed・failed・runtime_unsupported・runner_error・out_of_scope・blockedの件数</returns>
    internal static OutcomeCounts Count(IEnumerable<CaseResult> cases)
    {
        var counts = cases.CountBy(x => x.Outcome).ToDictionary();
        return new(
            counts.GetValueOrDefault(CaseOutcome.Passed),
            counts.GetValueOrDefault(CaseOutcome.Failed),
            counts.GetValueOrDefault(CaseOutcome.RuntimeUnsupported),
            counts.GetValueOrDefault(CaseOutcome.RunnerError),
            counts.GetValueOrDefault(CaseOutcome.OutOfScope),
            counts.GetValueOrDefault(CaseOutcome.Blocked)
        );
    }
}
