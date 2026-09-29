using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 全入力の変換結果を確定したmanifestと、その保存結果
/// </summary>
/// <param name="Manifest">確定した記録。保存に失敗した場合は出力の完了を記録しない</param>
/// <param name="ManifestPath">manifestの保存先の絶対path</param>
/// <param name="SaveFailure">保存を確定できなかった理由。保存に成功した場合はnull</param>
internal sealed record GenerateResult(
    CorpusManifest Manifest,
    string ManifestPath,
    ReportStoreException? SaveFailure
);
