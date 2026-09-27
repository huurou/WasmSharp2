namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// ソースの取得元と固定した版
/// </summary>
/// <param name="Url">取得元のリポジトリURL</param>
/// <param name="Commit">採用commit</param>
internal sealed record SourceRevision(string Url, string Commit);
