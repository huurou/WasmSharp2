using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 同じケース識別に対応付けた前後の結果
/// </summary>
/// <param name="Id">対応付けに使用したケース識別</param>
/// <param name="Change">前後の変化の種類</param>
/// <param name="Regression">以前passedだったケースが別分類へ変化または欠落したかどうか</param>
internal sealed record CaseComparison(CaseId Id, CaseChange Change, bool Regression)
{
    /// <summary>
    /// 比較元の結果 追加されたケースではnull
    /// </summary>
    public CaseResult? Baseline { get; init; }

    /// <summary>
    /// 現結果 欠落したケースではnull
    /// </summary>
    public CaseResult? Current { get; init; }
}
