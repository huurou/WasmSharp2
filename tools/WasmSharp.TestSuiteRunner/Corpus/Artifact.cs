namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 一つの生成物の同一性と所有元
/// </summary>
/// <param name="Path">manifest基準の/区切り相対path</param>
/// <param name="Kind">生成物の種類</param>
/// <param name="Sha256">生バイト列のSHA-256を表す小文字hex64桁</param>
/// <param name="InputPath">所有する元入力のtest/core基準の相対path</param>
internal sealed record Artifact(string Path, ArtifactKind Kind, string Sha256, string InputPath)
{
    /// <summary>
    /// JSONから取得できたcommand一覧と参照先 JSON以外または未読取ではnull
    /// </summary>
    public ScriptArtifact? Script { get; init; }
}
