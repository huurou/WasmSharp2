namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// commandが参照する名前と、対応する生成物の相対path
/// </summary>
/// <param name="CommandIndex">参照元commandの0始まりindex</param>
/// <param name="Filename">JSON内に記録された参照名</param>
/// <param name="ArtifactPath">参照名をJSONの配置に対して解決したmanifest基準の相対path</param>
internal sealed record ArtifactReference(int CommandIndex, string Filename, string ArtifactPath);
