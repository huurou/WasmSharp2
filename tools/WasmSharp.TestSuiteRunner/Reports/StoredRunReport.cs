using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 構造を読み取れた保存済み実行結果
/// </summary>
/// <param name="Report">読み取った実行結果</param>
/// <param name="Content">読み取った生バイト列</param>
/// <param name="Issues">記録の問題 問題がなければ空</param>
internal sealed record StoredRunReport(
    RunReport Report,
    byte[] Content,
    ImmutableArray<RecordIssue> Issues
) : StoredReport(Content, Issues);
