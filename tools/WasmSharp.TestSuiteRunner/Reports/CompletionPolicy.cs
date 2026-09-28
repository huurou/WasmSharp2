using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 操作ごとの完了条件と用途の合格条件から終了値を決める純粋な判定
/// </summary>
/// <remarks>
/// 用途の合格条件を満たさない場合は1、操作を完了できない場合は2とし、両方に該当する場合は2を優先する。
/// </remarks>
internal static class CompletionPolicy
{
    /// <summary>
    /// 全対象の変換・照合・記録・保存が完了し、runner_errorと未処理がない場合だけ成功とする。
    /// </summary>
    internal static CompletionDecision Generate(
        CorpusManifest manifest,
        ImmutableArray<RecordIssue> issues,
        bool saved
    )
    {
        List<DecisionReason> reasons = [];
        AddSaveFailure(reasons, saved);
        AddIssues(reasons, issues, _ => CompletionStatus.Incomplete);
        AddCount(
            reasons,
            manifest.Inputs.Count(x => x.Status == ConversionStatus.RunnerError),
            "変換・照合に失敗した入力"
        );
        AddCount(reasons, manifest.Diagnostics.Count, "操作全体の診断");
        return Decide(reasons);
    }

    /// <summary>
    /// 全対象の記録・保存が完了し、failedと入力単位・command単位のrunner_errorがない場合だけ成功とする。
    /// runtime_unsupported、out_of_scope、runtime_unsupportedだけを原因とするblockedは許容する。
    /// </summary>
    internal static CompletionDecision Run(
        RunReport report,
        ImmutableArray<RecordIssue> issues,
        bool saved
    )
    {
        List<DecisionReason> reasons = [];
        AddSaveFailure(reasons, saved);
        // 件数未確定は入力異常を記録し終えた状態であり、処理の中断とは区別して不合格にとどめる。
        AddIssues(
            reasons,
            issues,
            x =>
                x == RecordIssueKind.Undetermined
                    ? CompletionStatus.Unsatisfied
                    : CompletionStatus.Incomplete
        );
        var cases = report.Inputs.SelectMany(x => x.Cases).ToArray();
        var casesById = cases.ToLookup(x => x.Id);
        AddCount(reasons, cases.Count(x => x.Outcome == CaseOutcome.Failed), "failed");
        AddCount(
            reasons,
            cases.Count(x => x.Outcome == CaseOutcome.RunnerError),
            "command単位のrunner_error"
        );
        AddCount(reasons, report.Inputs.Sum(x => x.Issues.Count), "入力単位のrunner_error");
        AddCount(reasons, report.Diagnostics.Count, "操作全体の診断");
        AddCount(
            reasons,
            cases.Count(x =>
                x.Outcome == CaseOutcome.Blocked && !IsBlockedByUnsupported(x, casesById)
            ),
            "runtime_unsupported以外を原因とするblocked"
        );
        return Decide(reasons);
    }

    /// <summary>
    /// 記録を完了した結果だけの保存を成功とする。failedやrunner_errorの有無は問わない。
    /// </summary>
    internal static CompletionDecision BaselineSave(ImmutableArray<RecordIssue> issues, bool saved)
    {
        List<DecisionReason> reasons = [];
        AddSaveFailure(reasons, saved);
        AddIssues(reasons, issues, _ => CompletionStatus.Incomplete);
        return Decide(reasons);
    }

    /// <summary>
    /// 全比較・記録・保存が完了し、比較対象が一致し、現結果のrunner_errorがない場合だけ成功とする。
    /// 変換器実行ファイルのhashなどの出典差異は許容する。
    /// </summary>
    internal static CompletionDecision CompareConversion(ComparisonReport report, bool saved)
    {
        List<DecisionReason> reasons = [];
        AddSaveFailure(reasons, saved);
        AddComparisonCompletion(reasons, report);
        AddCount(reasons, report.Summary.ConditionDifferenceCount, "比較対象の条件差");
        AddCount(reasons, report.Summary.EntryDifferenceCount, "入力・生成物の差");
        AddCount(reasons, report.Summary.CurrentRunnerErrorCount, "現結果のrunner_error");
        return Decide(reasons);
    }

    /// <summary>
    /// 比較が成立して全比較・記録・保存が完了し、回帰と現結果のfailed・runner_errorがない場合だけ成功とする。
    /// 回帰がなくても既知のfailedが残る場合は成功としない。
    /// </summary>
    internal static CompletionDecision CompareRun(ComparisonReport report, bool saved)
    {
        List<DecisionReason> reasons = [];
        AddSaveFailure(reasons, saved);
        AddComparisonCompletion(reasons, report);
        AddCount(reasons, report.Summary.RegressionCount, "回帰");
        AddCount(reasons, report.Summary.CurrentFailedCount, "現結果のfailed");
        AddCount(
            reasons,
            report.Summary.CurrentRunnerErrorCount,
            "現結果の入力単位・command単位のrunner_error"
        );
        return Decide(reasons);
    }

    /// <summary>
    /// 単一の実行結果が固定profileの全入力・全commandを記録し、out_of_scope以外が全てpassedの場合だけ成功とする。
    /// 読み取れた結果の不成立は、未完了の記録も含めて用途の不合格として理由を返す。
    /// </summary>
    internal static CompletionDecision Verify(
        RunReport report,
        ImmutableArray<RecordIssue> issues,
        Core2Profile profile
    )
    {
        List<DecisionReason> reasons = [];
        if (!report.Corpus.Profile.Matches(profile))
        {
            reasons.Add(
                new(
                    CompletionStatus.Unsatisfied,
                    $"固定profile {profile.Id}と条件が一致しない結果です。"
                )
            );
        }

        AddIssues(reasons, issues, _ => CompletionStatus.Unsatisfied);
        AddCount(reasons, report.Inputs.Sum(x => x.Issues.Count), "入力単位のrunner_error");
        AddCount(reasons, report.Diagnostics.Count, "操作全体の診断");
        var cases = report.Inputs.SelectMany(x => x.Cases).ToArray();
        foreach (
            var (outcome, name) in new[]
            {
                (CaseOutcome.Failed, "failed"),
                (CaseOutcome.RuntimeUnsupported, "runtime_unsupported"),
                (CaseOutcome.RunnerError, "command単位のrunner_error"),
                (CaseOutcome.Blocked, "blocked"),
            }
        )
        {
            AddCount(reasons, cases.Count(x => x.Outcome == outcome), name);
        }

        return Decide(reasons);
    }

    private static bool IsBlockedByUnsupported(
        CaseResult item,
        ILookup<CaseId, CaseResult> casesById
    )
    {
        return item.Cause is { Origins.Count: > 0 } cause
            && cause.Origins.All(x =>
                casesById[x].Any()
                && casesById[x].All(y => y.Outcome == CaseOutcome.RuntimeUnsupported)
            );
    }

    private static void AddSaveFailure(List<DecisionReason> reasons, bool saved)
    {
        if (!saved)
        {
            reasons.Add(new(CompletionStatus.Incomplete, "結果の保存を完了できませんでした。"));
        }
    }

    private static void AddIssues(
        List<DecisionReason> reasons,
        ImmutableArray<RecordIssue> issues,
        Func<RecordIssueKind, CompletionStatus> getStatus
    )
    {
        // 大量の欠落でも表示が膨らまないよう、種類ごとに代表例と件数へまとめる。
        foreach (var group in issues.GroupBy(x => x.Kind))
        {
            var first = group.First().Message;
            var count = group.Count();
            reasons.Add(
                new(getStatus(group.Key), count == 1 ? first : $"{first}ほか{count - 1}件")
            );
        }
    }

    private static void AddComparisonCompletion(
        List<DecisionReason> reasons,
        ComparisonReport report
    )
    {
        if (!report.Established)
        {
            reasons.Add(new(CompletionStatus.Incomplete, "比較成立の条件を満たしていません。"));
        }

        if (!report.Complete)
        {
            reasons.Add(new(CompletionStatus.Incomplete, "比較が完了していません。"));
        }

        if (report.Summary.UncomparedCount > 0)
        {
            reasons.Add(
                new(
                    CompletionStatus.Incomplete,
                    $"比較できなかった対象が{report.Summary.UncomparedCount}件あります。"
                )
            );
        }
    }

    private static void AddCount(List<DecisionReason> reasons, int count, string name)
    {
        if (count > 0)
        {
            reasons.Add(new(CompletionStatus.Unsatisfied, $"{name}が{count}件あります。"));
        }
    }

    private static CompletionDecision Decide(List<DecisionReason> reasons)
    {
        return new(
            reasons.Count == 0 ? CompletionStatus.Succeeded : reasons.Max(x => x.Status),
            [.. reasons.Select(x => x.Message)]
        );
    }

    /// <summary>
    /// 終了状態に寄与する一つの理由
    /// </summary>
    /// <param name="Status">この理由による終了状態</param>
    /// <param name="Message">日本語の理由</param>
    private sealed record DecisionReason(CompletionStatus Status, string Message);
}
