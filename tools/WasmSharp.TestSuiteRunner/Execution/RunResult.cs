using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 全入力の実行記録と、その確定保存の結果
/// </summary>
/// <param name="Report">保存に失敗した場合は出力未完了とした実行記録</param>
/// <param name="OutputPath">詳細結果の保存先の絶対path</param>
/// <param name="SaveFailure">保存先と理由を持つ保存失敗 成功した場合はnull</param>
internal sealed record RunResult(
    RunReport Report,
    string OutputPath,
    ReportStoreException? SaveFailure
);
