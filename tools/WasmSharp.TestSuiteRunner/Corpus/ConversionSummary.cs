namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 全対象入力に対する変換状態と素材数の保存用集計
/// </summary>
/// <param name="InputCount">本来の全対象入力数</param>
/// <param name="SucceededCount">変換と照合に成功した入力数</param>
/// <param name="RunnerErrorCount">変換・読取・照合に失敗した入力数</param>
/// <param name="UnprocessedCount">処理を開始していない入力数</param>
/// <param name="ArtifactCount">部分生成を含む記録済み生成物数</param>
internal sealed record ConversionSummary(
    int InputCount,
    int SucceededCount,
    int RunnerErrorCount,
    int UnprocessedCount,
    int ArtifactCount
);
