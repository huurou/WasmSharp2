namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 構造を読める結果の記録に見つかった問題
/// </summary>
/// <param name="Kind">問題の種類</param>
/// <param name="Message">対象の入力やケースを含む日本語の説明</param>
internal sealed record RecordIssue(RecordIssueKind Kind, string Message);
