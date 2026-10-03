namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 生成成功・失敗・未処理を含む全対象と素材の保存用記録
/// </summary>
/// <param name="Profile">生成物が欠けても対象集合を失わない固定条件</param>
/// <param name="Provenance">生成条件とは分離して保持する実行時の参考出典</param>
internal sealed record CorpusManifest(ProfileSnapshot Profile, ConversionProvenance Provenance)
{
    /// <summary>
    /// 永続形式の版
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// 保存結果の種類
    /// </summary>
    public string Kind { get; init; } = "corpus_manifest";

    /// <summary>
    /// 未処理を含む入力ごとの変換結果
    /// </summary>
    public List<InputConversionResult> Inputs { get; init; } = [];

    /// <summary>
    /// 生成前提の不成立や中断など、操作全体に対する診断
    /// </summary>
    public List<CorpusDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// 入力の変換状態と生成物の集計
    /// </summary>
    public ConversionSummary Summary { get; init; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// 全入力の処理完了と出力の確定を分けた記録
    /// </summary>
    public ConversionCompletion Completion { get; init; } = new(false, false);

    /// <summary>
    /// 生成開始前に全入力を未処理として記録し、本来の対象集合を確定する。
    /// </summary>
    /// <param name="profile">公式入力の全対象と変換条件を固定するprofile</param>
    /// <param name="provenance">生成前提の確認で観測した環境と取得元</param>
    /// <returns>profileの一覧をコピーし、全入力を未処理、処理と出力を未完了とした生成記録</returns>
    internal static CorpusManifest Create(Core2Profile profile, ConversionProvenance provenance)
    {
        return new(ProfileSnapshot.FromProfile(profile), provenance)
        {
            Inputs = [.. profile.Inputs.Select(x => new InputConversionResult(x))],
            Summary = new(profile.Inputs.Length, 0, 0, profile.Inputs.Length, 0),
        };
    }

    /// <summary>
    /// 記録内容から、本来の対象入力数と変換状態・生成物数の集計を求める。
    /// </summary>
    /// <returns>固定した対象入力数と、現在の入力記録に含まれる各状態および部分生成物の件数</returns>
    internal ConversionSummary Summarize()
    {
        return new(
            Profile.Inputs.Count,
            Inputs.Count(x => x.Status == ConversionStatus.Succeeded),
            Inputs.Count(x => x.Status == ConversionStatus.RunnerError),
            Inputs.Count(x => x.Status == ConversionStatus.Unprocessed),
            Inputs.Sum(x => x.Artifacts.Count)
        );
    }

    /// <summary>
    /// 実行結果へ渡すため、入れ子を含むすべての可変一覧をコピーする。
    /// </summary>
    /// <returns>内容を維持し、元のmanifestとの間で可変一覧を共有しない生成記録</returns>
    internal CorpusManifest CreateSnapshot()
    {
        return this with
        {
            Profile = Profile.CreateSnapshot(),
            Diagnostics = [.. Diagnostics],
            Inputs =
            [
                .. Inputs.Select(x =>
                    x with
                    {
                        Arguments = [.. x.Arguments],
                        Diagnostics = [.. x.Diagnostics],
                        Artifacts =
                        [
                            .. x.Artifacts.Select(y =>
                                y with
                                {
                                    Script = y.Script is { } script
                                        ? script with
                                        {
                                            Commands = [.. script.Commands],
                                            References = [.. script.References],
                                        }
                                        : null,
                                }
                            ),
                        ],
                    }
                ),
            ],
        };
    }
}
