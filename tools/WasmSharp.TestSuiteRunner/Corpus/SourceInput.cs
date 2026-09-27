namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 生成物の有無にかかわらず対象に含める公式入力
/// </summary>
/// <param name="Path">test/core基準の/区切り相対path</param>
/// <param name="Sha256">固定Git blobの生バイト列のSHA-256を表す小文字hex64桁</param>
internal sealed record SourceInput(string Path, string Sha256);
