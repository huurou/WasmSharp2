using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// manifestと素材の照合結果
/// </summary>
/// <param name="Inputs">manifestの記録順の入力ごとの照合結果</param>
/// <param name="Diagnostics">特定の入力に属さない余剰の素材・元入力などの診断</param>
internal sealed record CorpusVerification(
    ImmutableArray<InputVerification> Inputs,
    ImmutableArray<CorpusDiagnostic> Diagnostics
);
