namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 記録の問題の種類
/// </summary>
internal enum RecordIssueKind
{
    /// <summary>
    /// 中断などにより処理していない入力・commandが残っている
    /// </summary>
    Unprocessed,

    /// <summary>
    /// 処理した入力のcommand件数が未確定である
    /// </summary>
    Undetermined,

    /// <summary>
    /// 出力の完了が記録されていない
    /// </summary>
    OutputFailed,

    /// <summary>
    /// 本来存在する入力・ケースの記録がない
    /// </summary>
    Missing,

    /// <summary>
    /// 同じ入力・ケースの記録が複数ある
    /// </summary>
    Duplicate,

    /// <summary>
    /// 対象外の入力や処理範囲外のケースが記録されている
    /// </summary>
    Unexpected,

    /// <summary>
    /// 件数・集計・完了情報が記録内容と整合しない
    /// </summary>
    Inconsistent,
}
