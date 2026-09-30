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
    /// <param name="manifest">判定する変換結果</param>
    /// <param name="issues">変換結果の記録内容に対する完全性検証の問題一覧</param>
    /// <param name="saved">結果の保存を完了できたかどうか</param>
    /// <returns>終了状態と、成功しない場合の理由</returns>
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
    /// <param name="report">判定する実行結果</param>
    /// <param name="issues">実行結果の記録内容に対する完全性検証の問題一覧</param>
    /// <param name="saved">結果の保存を完了できたかどうか</param>
    /// <returns>終了状態と理由 command件数未確定は不合格、それ以外の記録不備は未完了とする</returns>
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
    /// <param name="issues">保存元の記録内容に対する完全性検証の問題一覧</param>
    /// <param name="saved">baselineの保存を完了できたかどうか</param>
    /// <returns>記録と保存の完了状態、および完了できなかった理由</returns>
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
    /// <param name="report">変換manifestの比較結果</param>
    /// <param name="saved">比較結果の保存を完了できたかどうか</param>
    /// <returns>終了状態と、条件差・素材差・runner_errorまたは未完了の理由</returns>
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
    /// <param name="report">実行結果の比較結果</param>
    /// <param name="saved">比較結果の保存を完了できたかどうか</param>
    /// <returns>終了状態と、回帰・failed・runner_errorまたは未完了の理由</returns>
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
    /// <param name="report">公式ケースの受入判定に使用する実行結果</param>
    /// <param name="issues">実行結果の記録内容に対する完全性検証の問題一覧</param>
    /// <param name="profile">受入対象となる固定Core2.0の条件と入力集合</param>
    /// <returns>合格または不合格の終了状態と、条件不一致・記録不備・不合格ケースの理由</returns>
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

    /// <summary>
    /// blockedの起点がすべて記録済みのruntime_unsupportedであるかを判定する。
    /// </summary>
    /// <param name="item">依存原因を調べるケース結果</param>
    /// <param name="casesById">起点の識別から結果を参照する全ケースの対応表</param>
    /// <returns>起点が1件以上あり、全起点の全記録がruntime_unsupportedの場合はtrue</returns>
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

    /// <summary>
    /// 保存に失敗した場合に、操作未完了の理由を追加する。
    /// </summary>
    /// <param name="reasons">判定理由の追加先</param>
    /// <param name="saved">保存を完了できたかどうか</param>
    private static void AddSaveFailure(List<DecisionReason> reasons, bool saved)
    {
        if (!saved)
        {
            reasons.Add(new(CompletionStatus.Incomplete, "結果の保存を完了できませんでした。"));
        }
    }

    /// <summary>
    /// 記録の問題を種類ごとの代表例と件数にまとめ、用途に応じた終了状態の理由を追加する。
    /// </summary>
    /// <param name="reasons">判定理由の追加先</param>
    /// <param name="issues">記録内容の問題一覧</param>
    /// <param name="getStatus">問題の種類から、この用途での終了状態を決める処理</param>
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

    /// <summary>
    /// 比較条件の不成立、比較の未完了、未比較対象を操作未完了の理由として追加する。
    /// </summary>
    /// <param name="reasons">判定理由の追加先</param>
    /// <param name="report">完了状態を調べる比較結果</param>
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

    /// <summary>
    /// 不合格の対象が1件以上ある場合に、件数を伴う理由を追加する。
    /// </summary>
    /// <param name="reasons">判定理由の追加先</param>
    /// <param name="count">不合格の対象件数</param>
    /// <param name="name">対象の種類を表す表示名</param>
    private static void AddCount(List<DecisionReason> reasons, int count, string name)
    {
        if (count > 0)
        {
            reasons.Add(new(CompletionStatus.Unsatisfied, $"{name}が{count}件あります。"));
        }
    }

    /// <summary>
    /// 理由がない場合は成功、理由がある場合は未完了を優先した終了状態を返す。
    /// </summary>
    /// <param name="reasons">終了状態に寄与する理由一覧</param>
    /// <returns>最も優先度が高い終了状態と、入力順の理由メッセージ</returns>
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
