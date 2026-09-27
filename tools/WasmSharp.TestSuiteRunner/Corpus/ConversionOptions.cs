namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 変換結果に影響するWABTの固定option
/// </summary>
/// <param name="Check">変換時の検証を行うかどうか</param>
/// <param name="CanonicalLebs">LEBを最短表現で出力するかどうか</param>
/// <param name="Relocatable">再配置可能なbinaryを生成するかどうか</param>
/// <param name="DebugNames">デバッグ名を出力するかどうか</param>
internal sealed record ConversionOptions(
    bool Check,
    bool CanonicalLebs,
    bool Relocatable,
    bool DebugNames
);
