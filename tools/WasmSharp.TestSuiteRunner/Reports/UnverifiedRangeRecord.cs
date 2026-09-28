namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 構文検査または検証が完了していない入力範囲
/// </summary>
/// <param name="Stage">完了していないランタイムの処理段階の列挙名</param>
/// <param name="StartOffset">範囲の先頭位置</param>
/// <param name="EndOffset">範囲に含まない終端位置</param>
/// <param name="Description">完了していない内容の説明</param>
internal sealed record UnverifiedRangeRecord(
    string Stage,
    long StartOffset,
    long EndOffset,
    string Description
);
