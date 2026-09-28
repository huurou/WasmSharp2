namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 比較結果の保存用集計
/// </summary>
/// <param name="ConditionDifferenceCount">条件差の件数</param>
/// <param name="ProvenanceDifferenceCount">出典差の件数</param>
/// <param name="EntryDifferenceCount">入力・生成物の差の件数</param>
/// <param name="ChangedCaseCount">変化したケース数</param>
/// <param name="AddedCaseCount">追加されたケース数</param>
/// <param name="MissingCaseCount">欠落したケース数</param>
/// <param name="RegressionCount">回帰したケース数</param>
/// <param name="UncomparedCount">比較できなかった対象の数</param>
/// <param name="CurrentFailedCount">現結果のfailedの件数</param>
/// <param name="CurrentRunnerErrorCount">現結果の入力単位・command単位のrunner_errorの件数</param>
internal sealed record ComparisonSummary(
    int ConditionDifferenceCount,
    int ProvenanceDifferenceCount,
    int EntryDifferenceCount,
    int ChangedCaseCount,
    int AddedCaseCount,
    int MissingCaseCount,
    int RegressionCount,
    int UncomparedCount,
    int CurrentFailedCount,
    int CurrentRunnerErrorCount
);
