namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// blockedの直接原因と、元の非blocked分類に至る参照
/// </summary>
internal sealed record CaseCause
{
    /// <summary>
    /// 同じ入力の先行commandのうち、実行に必要だった利用不能状態の原因
    /// </summary>
    public List<CaseId> Direct { get; init; } = [];

    /// <summary>
    /// 直接原因をたどって到達する、blocked以外に分類された元の失敗command
    /// </summary>
    public List<CaseId> Origins { get; init; } = [];
}
