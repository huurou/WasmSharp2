using System.Text.Json;
using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 保存JSONだけで入力集合と変換条件を復元できるprofileのDTO
/// </summary>
/// <param name="Id">profileの識別名</param>
/// <param name="Spec">公式仕様の取得元と採用commit</param>
/// <param name="Wabt">変換器ソースの取得元と採用commit</param>
/// <param name="WorkingDirectory">spec-root基準の変換作業ディレクトリ</param>
/// <param name="Conversion">生成物に影響する固定option</param>
internal sealed record ProfileSnapshot(
    string Id,
    SourceRevision Spec,
    SourceRevision? Wabt,
    string WorkingDirectory,
    ConversionOptions? Conversion
) : IJsonOnDeserialized
{
    /// <summary>
    /// wasm-toolsを使う場合の固定リリース Core 2.0の保存形式には出力しない
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WasmToolsRelease? WasmTools { get; init; }

    /// <summary>
    /// 対象機能の状態と、変換器に機能切替がある場合の既定値
    /// </summary>
    public List<FeatureSetting> Features { get; init; } = [];

    /// <summary>
    /// 生成済み素材の有無に依存しない全入力と生バイトSHA-256
    /// </summary>
    public List<SourceInput> Inputs { get; init; } = [];

    /// <summary>
    /// 実際の配置rootを含まない変換引数のテンプレート
    /// </summary>
    public List<string> LogicalArguments { get; init; } = [];

    void IJsonOnDeserialized.OnDeserialized()
    {
        // Core 3.0用にnullを表せる型にしても、従来のWABT形式では必須値を欠いた記録を受け付けない。
        if (WasmTools is null && (Wabt is null || Conversion is null || Features.Any(x => x is { DefaultEnabled: null })))
        {
            throw new JsonException("WABTのprofileにはwabt、conversion、機能のdefault_enabledが必要です。");
        }
    }

    /// <summary>
    /// 不変モデルの全条件を、独立した一覧を持つ保存用DTOへコピーする。
    /// </summary>
    /// <param name="profile">保存する公式入力の全対象と変換条件</param>
    /// <returns>feature、入力、論理引数をそれぞれ新しい一覧へコピーした保存用profile</returns>
    internal static ProfileSnapshot FromProfile(ConversionProfile profile)
    {
        return new ProfileSnapshot(profile.Id, profile.Spec, profile.Wabt, profile.WorkingDirectory, profile.Conversion)
        {
            WasmTools = profile.WasmTools,
            Features = [.. profile.Features],
            Inputs = [.. profile.Inputs],
            LogicalArguments = [.. profile.LogicalArguments],
        };
    }

    /// <summary>
    /// 保存用DTOの一覧をコピーし、不変のprofileを復元する。
    /// </summary>
    /// <returns>可変一覧の内容を不変配列へコピーしたprofile</returns>
    internal ConversionProfile ToProfile()
    {
        return new ConversionProfile(Id, Spec, Wabt, [.. Features], [.. Inputs], WorkingDirectory, [.. LogicalArguments], Conversion)
        {
            WasmTools = WasmTools,
        };
    }

    /// <summary>
    /// 保存された条件が、指定したprofileの識別・版・全feature・全入力・変換条件と一致するかを判定する。
    /// </summary>
    /// <param name="profile">比較する固定profile</param>
    /// <returns>feature、入力、論理引数の順序を含め、すべての条件が一致する場合はtrue</returns>
    internal bool Matches(ConversionProfile profile)
    {
        return Id == profile.Id
            && Spec == profile.Spec
            && Wabt == profile.Wabt
            && WasmTools == profile.WasmTools
            && WorkingDirectory == profile.WorkingDirectory
            && Conversion == profile.Conversion
            && Features.SequenceEqual(profile.Features)
            && Inputs.SequenceEqual(profile.Inputs)
            && LogicalArguments.SequenceEqual(profile.LogicalArguments);
    }

    /// <summary>
    /// 保存内容を維持したまま、可変一覧を独立させる。
    /// </summary>
    /// <returns>feature、入力、論理引数をそれぞれ新しい一覧へコピーした保存用profile</returns>
    internal ProfileSnapshot CreateSnapshot()
    {
        return this with { Features = [.. Features], Inputs = [.. Inputs], LogicalArguments = [.. LogicalArguments] };
    }
}
