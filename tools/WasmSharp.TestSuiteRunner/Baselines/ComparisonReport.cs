namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 保存済みの2つの結果を比較した成立・完了・差分の保存用記録 比較元は変更しない
/// </summary>
/// <param name="Comparison">変換結果の再現性比較か、実行結果の回帰比較か</param>
/// <param name="Baseline">比較元baselineの識別</param>
/// <param name="Current">比較する現結果の識別</param>
internal sealed record ComparisonReport(
    ComparisonType Comparison,
    ComparedReport Baseline,
    ComparedReport Current
)
{
    /// <summary>
    /// 永続形式の版
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// 保存結果の種類
    /// </summary>
    public string Kind { get; init; } = "comparison_report";

    /// <summary>
    /// 比較成立の条件を満たしたかどうか
    /// </summary>
    public bool Established { get; init; }

    /// <summary>
    /// 未比較の対象を残さず全対象を比較したかどうか
    /// </summary>
    public bool Complete { get; init; }

    /// <summary>
    /// profile・commit・feature・論理引数など、比較対象の同一性に含める条件の差
    /// </summary>
    public List<ConditionDifference> ConditionDifferences { get; init; } = [];

    /// <summary>
    /// 変換器実行ファイルのhashなど、比較対象の不一致として扱わない出典の差
    /// </summary>
    public List<ConditionDifference> ProvenanceDifferences { get; init; } = [];

    /// <summary>
    /// 入力・生成物の有無・hash・変換状態の差
    /// </summary>
    public List<EntryDifference> EntryDifferences { get; init; } = [];

    /// <summary>
    /// 対応付けたケースの前後の結果と回帰
    /// </summary>
    public List<CaseComparison> Cases { get; init; } = [];

    /// <summary>
    /// 比較できなかった対象と理由
    /// </summary>
    public List<UncomparedItem> Uncompared { get; init; } = [];

    /// <summary>
    /// 差分・回帰・現結果の分類の集計
    /// </summary>
    public ComparisonSummary Summary { get; init; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
