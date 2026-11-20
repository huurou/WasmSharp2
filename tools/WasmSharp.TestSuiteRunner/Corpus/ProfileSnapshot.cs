using System.Text.Json;
using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 保存JSONだけで入力集合と変換条件を復元できるprofileのDTO
/// </summary>
/// <param name="Id">profileの識別名</param>
/// <param name="Spec">公式仕様の取得元と採用commit</param>
/// <param name="Wabt">WABTを使う場合の取得元と採用commit</param>
/// <param name="WorkingDirectory">spec-root基準の変換作業ディレクトリ</param>
/// <param name="Conversion">WABTを使う場合の生成物に影響する固定オプション</param>
internal sealed record ProfileSnapshot(
    string Id,
    SourceRevision Spec,
    SourceRevision? Wabt,
    string WorkingDirectory,
    ConversionOptions? Conversion
) : IJsonOnDeserialized
{
    /// <summary>
    /// wasm-toolsを使う場合の固定リリース WABTの保存形式には出力しない
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
        if (WasmTools is not null)
        {
            if (Wabt is not null || Conversion is not null)
            {
                throw new JsonException("wasm_toolsとWABTの変換条件は併用できません。");
            }
            return;
        }
        if (Wabt is null)
        {
            throw new JsonException("WABTのprofileにはwabtが必要です。");
        }
        if (Conversion is null)
        {
            throw new JsonException("WABTのprofileにはconversionが必要です。");
        }
        if (Features.Find(x => x is { DefaultEnabled: null }) is { } feature)
        {
            throw new JsonException($"WABTのprofileにはfeatures[{feature.Name}].default_enabledが必要です。");
        }
    }

    /// <summary>
    /// 不変モデルの全条件を、独立した一覧を持つ保存用DTOへコピーする。
    /// </summary>
    /// <param name="profile">保存する公式入力の全対象と変換条件</param>
    /// <returns>feature、入力、論理引数をそれぞれ新しい一覧へコピーした保存用profile</returns>
    internal static ProfileSnapshot FromProfile(ConversionProfile profile)
    {
        return new ProfileSnapshot(
            profile.Id,
            profile.Spec,
            profile.Wabt?.Revision,
            profile.WorkingDirectory,
            profile.Wabt?.Options
        )
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
        var wabt =
            Wabt is { } revision && Conversion is { } options ? new ConversionProfile.WabtSettings(revision, options) : null;
        return new ConversionProfile(Id, Spec, wabt, [.. Features], [.. Inputs], WorkingDirectory, [.. LogicalArguments])
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
            && Wabt == profile.Wabt?.Revision
            && WasmTools == profile.WasmTools
            && WorkingDirectory == profile.WorkingDirectory
            && Conversion == profile.Wabt?.Options
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
