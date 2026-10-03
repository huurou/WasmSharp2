namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 素材同一性の条件に含めない、変換時の環境と取得元の記録
/// </summary>
internal sealed record ConversionProvenance
{
    /// <summary>
    /// 変換器のビルド条件を代表する実行ファイルのSHA-256 実行ファイルを読み取れない場合はnull
    /// </summary>
    public string? ExecutableSha256 { get; init; }

    /// <summary>
    /// 実際に起動する変換器の絶対path
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// 生成記録を開始した日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 生成環境のOS
    /// </summary>
    public string? OperatingSystem { get; init; }

    /// <summary>
    /// 生成環境のアーキテクチャ
    /// </summary>
    public string? Architecture { get; init; }

    /// <summary>
    /// 実際の公式入力の配置root
    /// </summary>
    public string? SpecRoot { get; init; }

    /// <summary>
    /// 実際の変換器ソースの配置root
    /// </summary>
    public string? WabtRoot { get; init; }

    /// <summary>
    /// CLIで解決した出力先の絶対path
    /// </summary>
    public string? OutputRoot { get; init; }

    /// <summary>
    /// 公式入力の実際のorigin 管理外コピーや取得できない場合はnull
    /// </summary>
    public string? SpecOrigin { get; init; }

    /// <summary>
    /// 変換器ソースの実際のorigin 取得できない場合はnull
    /// </summary>
    public string? WabtOrigin { get; init; }

    /// <summary>
    /// 公式入力の配置自体がGit checkoutの場合に観測したHEAD 管理外コピーや取得できない場合はnull
    /// </summary>
    public string? SpecHead { get; init; }

    /// <summary>
    /// 変換器ソースのcheckoutで観測したHEAD 取得できない場合はnull
    /// </summary>
    public string? WabtHead { get; init; }
}
