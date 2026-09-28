using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 構造を読み取れた保存済みmanifest
/// </summary>
/// <param name="Manifest">読み取ったmanifest</param>
/// <param name="Content">読み取った生バイト列</param>
/// <param name="Issues">記録の問題。問題がなければ空</param>
internal sealed record StoredManifest(
    CorpusManifest Manifest,
    byte[] Content,
    ImmutableArray<RecordIssue> Issues
) : StoredReport(Content, Issues);
