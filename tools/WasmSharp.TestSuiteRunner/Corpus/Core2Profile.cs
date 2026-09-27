using System.Collections.Immutable;
using System.Text.Json;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 公式入力と変換条件を固定する不変のprofile
/// </summary>
/// <param name="Id">profileの識別名</param>
/// <param name="Spec">公式仕様の取得元と採用commit</param>
/// <param name="Wabt">変換器ソースの取得元と採用commit</param>
/// <param name="Features">固定WABTの全機能の既定値と実効値</param>
/// <param name="Inputs">test/core基準のOrdinal順の全入力と生バイトSHA-256</param>
/// <param name="WorkingDirectory">spec-root基準の変換作業ディレクトリ</param>
/// <param name="LogicalArguments">配置rootを含まない変換引数のテンプレート</param>
/// <param name="Conversion">生成物に影響する固定option</param>
internal sealed record Core2Profile(
    string Id,
    SourceRevision Spec,
    SourceRevision Wabt,
    ImmutableArray<FeatureSetting> Features,
    ImmutableArray<SourceInput> Inputs,
    string WorkingDirectory,
    ImmutableArray<string> LogicalArguments,
    ConversionOptions Conversion
)
{
    /// <summary>
    /// 埋込みprofileのsnake_case名に対応する読取設定
    /// </summary>
    private static readonly JsonSerializerOptions jsonOptions_ = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// 配置先やランタイムの実装状況に依存しない埋込みprofileを読み取る。
    /// </summary>
    internal static Core2Profile Load()
    {
        using var stream =
            typeof(Core2Profile).Assembly.GetManifestResourceStream("Core2.Profile")
            ?? throw new InvalidOperationException("固定Core 2.0 profileが見つかりません。");
        return Load(stream);
    }

    /// <summary>
    /// 同じ形式のprofileを読み取る。入力streamの所有権は呼び出し元に残る。
    /// </summary>
    internal static Core2Profile Load(Stream stream)
    {
        return JsonSerializer.Deserialize<Core2Profile>(stream, jsonOptions_)
            ?? throw new JsonException("profileがnullです。");
    }
}
