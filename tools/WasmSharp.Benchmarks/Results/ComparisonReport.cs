using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 保存済みrun群と変更前policyを指定する比較要求
/// </summary>
internal sealed record ComparisonRequest(
    List<string> BaselineRunIds,
    List<string> CurrentRunIds,
    string PolicyPath,
    string PolicySha256,
    List<string> CaseIds
);

/// <summary>
/// comparison.jsonに保存する入力の同定とケース別判定
/// </summary>
internal sealed record ComparisonReport(
    uint SchemaVersion,
    List<ComparedRun> BaselineRuns,
    List<ComparedRun> CurrentRuns,
    ManifestFile Policy,
    List<CaseComparison> Cases
);

/// <summary>
/// 比較に使用したrun IDとmanifestのpath・hash
/// </summary>
internal sealed record ComparedRun(string RunId, ManifestFile Manifest);

/// <summary>
/// 両指標の判定と条件差 対象外ケースの改善表示と選定した目標の達成を区別する。
/// </summary>
internal sealed record CaseComparison(
    string CaseId,
    ComparisonDecision Decision,
    bool TargetAchieved,
    bool BlocksCompletion,
    List<ComparisonReason> Reasons,
    List<ConditionDifference> ConditionDifferences,
    MetricComparison Time,
    MetricComparison Allocation
);

/// <summary>
/// 比較における4分類
/// </summary>
internal enum ComparisonDecision
{
    Improved,
    Equivalent,
    Regressed,
    Indeterminate,
}

/// <summary>
/// 条件の相違と、その値を採取したrun
/// </summary>
internal sealed record ConditionDifference(
    string Condition,
    string? Baseline,
    string? Current,
    string BaselineRunId,
    string CurrentRunId
);

/// <summary>
/// 各指標の独立run群・差・判定 必要値がない場合はnullを保持する。
/// </summary>
internal sealed record MetricComparison(
    RunGroupStatistics? Baseline,
    RunGroupStatistics? Current,
    MetricChange? Change,
    ComparisonDecision Decision,
    bool? WithinAllowedIncrease,
    bool TargetAchieved,
    List<ComparisonReason> Reasons
);

/// <summary>
/// 独立runの値と中央値・最小最大 時間は各runの平均ns/op、割り当ては丸め前bytes/opを用いる。
/// </summary>
internal sealed record RunGroupStatistics(
    List<RunMetricValue> Runs,
    double Median,
    double Minimum,
    double Maximum
);

/// <summary>
/// 1runの値と、そのrun内で算出した信頼区間 割り当ての信頼区間はnull
/// </summary>
internal sealed record RunMetricValue(
    string RunId,
    double Value,
    ConfidenceInterval? ConfidenceInterval
);

/// <summary>
/// current-baselineの絶対差とcurrent/baselineの比率 baselineが0ならRatioはnull
/// </summary>
internal sealed record MetricChange(double AbsoluteDifference, double? Ratio);

/// <summary>
/// 判定を説明する理由の分類と具体的な説明
/// </summary>
internal sealed record ComparisonReason(ComparisonReasonKind Kind, string Description);

/// <summary>
/// 判定成立・不成立の理由 条件差等の比較不足と、許容内での目標未達を区別する。
/// </summary>
internal enum ComparisonReasonKind
{
    ConditionMismatch,
    CorrectnessFailure,
    InsufficientMeasurements,
    ExcessiveVariation,
    MissingPolicy,
    TargetNotReached,
    OutsideEquivalenceWithinAllowance,
    AllowedIncreaseExceeded,
    ReductionTargetReached,
    WithinEquivalence,
    ImprovementBeyondNoise,
}
