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
    SourceRevision Wabt,
    string WorkingDirectory,
    ConversionOptions Conversion
)
{
    /// <summary>
    /// 全featureの既定値と実効値
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

    /// <summary>
    /// 不変モデルの全条件を、独立した一覧を持つ保存用DTOへコピーする。
    /// </summary>
    /// <param name="profile">保存する公式入力の全対象と変換条件</param>
    /// <returns>feature、入力、論理引数をそれぞれ新しい一覧へコピーした保存用profile</returns>
    internal static ProfileSnapshot FromProfile(Core2Profile profile)
    {
        return new(
            profile.Id,
            profile.Spec,
            profile.Wabt,
            profile.WorkingDirectory,
            profile.Conversion
        )
        {
            Features = [.. profile.Features],
            Inputs = [.. profile.Inputs],
            LogicalArguments = [.. profile.LogicalArguments],
        };
    }

    /// <summary>
    /// 保存用DTOの一覧をコピーし、不変のprofileを復元する。
    /// </summary>
    /// <returns>可変一覧の内容を不変配列へコピーしたprofile</returns>
    internal Core2Profile ToProfile()
    {
        return new(
            Id,
            Spec,
            Wabt,
            [.. Features],
            [.. Inputs],
            WorkingDirectory,
            [.. LogicalArguments],
            Conversion
        );
    }

    /// <summary>
    /// 保存された条件が、指定したprofileの識別・版・全feature・全入力・変換条件と一致するかを判定する。
    /// </summary>
    /// <param name="profile">比較する固定profile</param>
    /// <returns>feature、入力、論理引数の順序を含め、すべての条件が一致する場合はtrue</returns>
    internal bool Matches(Core2Profile profile)
    {
        return Id == profile.Id
            && Spec == profile.Spec
            && Wabt == profile.Wabt
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
        return this with
        {
            Features = [.. Features],
            Inputs = [.. Inputs],
            LogicalArguments = [.. LogicalArguments],
        };
    }
}
