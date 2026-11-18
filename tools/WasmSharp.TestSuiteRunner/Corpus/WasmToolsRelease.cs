namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 素材変換に使用するwasm-toolsの固定リリース
/// </summary>
internal sealed record WasmToolsRelease(string Url, string Commit, string Version);
