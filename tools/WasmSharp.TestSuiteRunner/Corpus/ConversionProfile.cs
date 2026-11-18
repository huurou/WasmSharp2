using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 公式入力と変換条件を固定する不変のprofile
/// </summary>
/// <param name="Id">profileの識別名</param>
/// <param name="Spec">公式仕様の取得元と採用commit</param>
/// <param name="Wabt">変換器ソースの取得元と採用commit</param>
/// <param name="Features">対象機能の状態と、変換器に機能切替がある場合の既定値</param>
/// <param name="Inputs">test/core基準のOrdinal順の全入力と生バイトSHA-256</param>
/// <param name="WorkingDirectory">spec-root基準の変換作業ディレクトリ</param>
/// <param name="LogicalArguments">配置rootを含まない変換引数のテンプレート</param>
/// <param name="Conversion">生成物に影響する固定option</param>
internal sealed record ConversionProfile(
    string Id,
    SourceRevision Spec,
    SourceRevision? Wabt,
    ImmutableArray<FeatureSetting> Features,
    ImmutableArray<SourceInput> Inputs,
    string WorkingDirectory,
    ImmutableArray<string> LogicalArguments,
    ConversionOptions? Conversion
)
{
    internal WasmToolsRelease? WasmTools { get; init; }
}
