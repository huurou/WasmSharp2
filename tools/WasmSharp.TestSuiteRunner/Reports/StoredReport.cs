using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 構造を読み取れた保存済み結果と、その内容で見つかった記録の問題
/// </summary>
/// <param name="Content">読み取った生バイト列</param>
/// <param name="Issues">欠落・重複・未処理・集計不整合などの問題。問題がなければ空</param>
internal abstract record StoredReport(byte[] Content, ImmutableArray<RecordIssue> Issues);
