namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 一つの公式ケースの分類と、判断に用いた期待・観測・原因の保存用記録
/// </summary>
/// <param name="Id">入力相対pathとcommand indexによるケース識別</param>
/// <param name="Line">元入力の1始まり行番号 取得できない場合はnull</param>
/// <param name="CommandType">元JSONのcommand種別 取得できない場合はnull</param>
/// <param name="Category">集計区分 種類を確定できない場合はnull</param>
/// <param name="Outcome">6分類のいずれか</param>
internal sealed record CaseResult(
    CaseId Id,
    int? Line,
    string? CommandType,
    CaseCategory? Category,
    CaseOutcome Outcome
)
{
    /// <summary>
    /// 否定assertionの期待診断 JSONのtextを加工せず保持し、該当しない場合はnull
    /// </summary>
    public string? ExpectedText { get; init; }

    /// <summary>
    /// 値付き期待値、または型だけの結果宣言
    /// </summary>
    public List<ExpectedValueRecord> ExpectedValues { get; init; } = [];

    /// <summary>
    /// invoke/getで実際に得た値 個数・型の不一致時も全て保持する
    /// </summary>
    public List<ValueRecord> ActualValues { get; init; } = [];

    /// <summary>
    /// 最後に実行した段階 公開処理へ進む前に判定した場合はnull
    /// </summary>
    public CaseStage? LastStage { get; init; }

    /// <summary>
    /// 観測した失敗や、JSON・素材の異常の診断
    /// </summary>
    public List<CaseDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// このcommandの処理中に呼ばれたspectestのprintの呼出し順の記録
    /// </summary>
    public List<PrintRecord> Prints { get; init; } = [];

    /// <summary>
    /// blockedの原因 blocked以外ではnull
    /// </summary>
    public CaseCause? Cause { get; init; }
}
