namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// JSONの意味解釈に先立って列挙したcommandと素材参照の保存用記録
/// </summary>
internal sealed record ScriptArtifact
{
    /// <summary>
    /// JSONに記録されたsource_filename 取得できなかった場合はnull
    /// </summary>
    public string? SourceFilename { get; init; }

    /// <summary>
    /// JSON全体のcommand列挙を完了したかどうか
    /// </summary>
    public bool EnumerationComplete { get; init; }

    /// <summary>
    /// 確定したcommand総数 構文破損などで総数が未確定の場合はnull
    /// </summary>
    public int? CommandCount { get; init; }

    /// <summary>
    /// 境界を確定できたcommandの順序付き一覧
    /// </summary>
    public List<ArtifactCommand> Commands { get; init; } = [];

    /// <summary>
    /// 各commandのfilenameとmanifest上の生成物の対応
    /// </summary>
    public List<ArtifactReference> References { get; init; } = [];
}
