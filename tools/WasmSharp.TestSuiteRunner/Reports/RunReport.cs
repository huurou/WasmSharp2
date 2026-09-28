using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 全対象入力の実行結果と、比較成立の条件となる素材・実行条件の保存用記録
/// </summary>
/// <param name="Corpus">実行に使用したmanifestのスナップショット。profileと素材一覧/hashを含む</param>
/// <param name="Provenance">ケース同一性に含めない実行時の参考出典</param>
/// <param name="ExecutionPolicy">全instanceへ明示した実行ポリシー</param>
internal sealed record RunReport(
    CorpusManifest Corpus,
    RunProvenance Provenance,
    RunExecutionPolicy ExecutionPolicy
)
{
    /// <summary>
    /// 永続形式の版
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// 保存結果の種類
    /// </summary>
    public string Kind { get; init; } = "run_report";

    /// <summary>
    /// 未処理を含む入力ごとの実行結果
    /// </summary>
    public List<InputRunResult> Inputs { get; init; } = [];

    /// <summary>
    /// 中断など、特定の入力に属さない操作全体の診断
    /// </summary>
    public List<CorpusDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// 入力・command・6分類の集計
    /// </summary>
    public RunSummary Summary { get; init; } = RunSummary.Empty;

    /// <summary>
    /// 合格判定と区別する処理・出力の完了情報。保存時の値は読取側で再検証する
    /// </summary>
    public RunCompletion Completion { get; init; } = new(false, false);

    /// <summary>
    /// 実行開始前に全入力を未処理として記録し、素材のスナップショットと本来の対象集合を確定する。
    /// </summary>
    internal static RunReport Create(
        CorpusManifest manifest,
        RunProvenance provenance,
        RunExecutionPolicy executionPolicy
    )
    {
        var report = new RunReport(manifest.CreateSnapshot(), provenance, executionPolicy)
        {
            Inputs = [.. manifest.Profile.Inputs.Select(x => new InputRunResult(x.Path))],
        };
        return report with { Summary = report.Summarize() };
    }

    /// <summary>
    /// 記録内容から集計を求める。未処理のcommandは6分類に含めない。
    /// </summary>
    internal RunSummary Summarize()
    {
        var cases = Inputs.SelectMany(x => x.Cases).ToArray();
        return new(
            Corpus.Profile.Inputs.Count,
            Inputs.Count(x => x.Status == InputRunStatus.Processed),
            Inputs.Count(x => x.Status == InputRunStatus.Incomplete),
            Inputs.Count(x => x.Status == InputRunStatus.Unprocessed),
            Inputs.Count(x => x.Status != InputRunStatus.Unprocessed && x.CommandCount is null),
            Inputs.Count(x => x.Issues.Count > 0),
            Inputs.Sum(x => x.Issues.Count),
            Inputs.Sum(x => x.EnumeratedCount),
            Inputs.Sum(x => x.UnprocessedCount),
            OutcomeCounts.Count(cases.Where(x => x.Category == CaseCategory.Setup)),
            OutcomeCounts.Count(cases.Where(x => x.Category == CaseCategory.Action)),
            OutcomeCounts.Count(cases.Where(x => x.Category == CaseCategory.Assertion)),
            cases.Count(x => x.Category is null && x.Outcome == CaseOutcome.RunnerError)
        );
    }
}
