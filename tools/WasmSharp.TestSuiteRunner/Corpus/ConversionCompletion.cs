namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 合格判定と区別する処理・出力の完了情報 保存時の値は読取側で再検証する
/// </summary>
/// <param name="ProcessingComplete">失敗を含めて全入力の処理と記録を完了したかどうか</param>
/// <param name="OutputComplete">出力を最後まで書き切り確定したかどうか</param>
internal sealed record ConversionCompletion(bool ProcessingComplete, bool OutputComplete);
