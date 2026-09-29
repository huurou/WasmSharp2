using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 一つの入力について照合した素材と、入力別・素材別の異常
/// </summary>
/// <param name="Input">照合した入力</param>
/// <param name="Document">照合済みで実行に使えるJSON。JSONを信用できない場合はnull</param>
/// <param name="Issues">wat異常やJSON異常など、command単位の件数とは別に数える入力異常</param>
/// <param name="Modules">照合済みのbinaryを、それを利用するcommandのindexごとに保持する</param>
/// <param name="ModuleIssues">利用できないbinaryの素材別の理由を、それを利用するcommandのindexごとに保持する</param>
internal sealed record InputVerification(
    SourceInput Input,
    ScriptDocument? Document,
    ImmutableArray<CorpusDiagnostic> Issues,
    ImmutableDictionary<int, ImmutableArray<byte>> Modules,
    ImmutableDictionary<int, CorpusDiagnostic> ModuleIssues
);
