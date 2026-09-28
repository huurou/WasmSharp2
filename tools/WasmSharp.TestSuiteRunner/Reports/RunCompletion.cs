namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 合格判定と区別する処理・出力の完了情報。保存時の値は読取側で再検証する
/// </summary>
/// <param name="ProcessingComplete">入力異常やfailedを含めて全対象の処理と記録を完了したかどうか</param>
/// <param name="OutputComplete">保存内容として確定させる出力かどうか</param>
internal sealed record RunCompletion(bool ProcessingComplete, bool OutputComplete);
