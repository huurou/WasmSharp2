using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 変換を開始する前に確認した生成前提と、観測した参考出典
/// </summary>
/// <param name="Provenance">素材同一性の条件に含めない変換時の出典</param>
/// <param name="Diagnostics">生成前提の不成立 空の場合だけ変換を開始できる</param>
internal sealed record GenerationPreconditions(
    ConversionProvenance Provenance,
    ImmutableArray<CorpusDiagnostic> Diagnostics
);
