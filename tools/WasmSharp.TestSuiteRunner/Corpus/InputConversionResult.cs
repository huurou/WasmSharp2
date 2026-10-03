namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 一つの公式入力の変換状態と、部分生成を含む生成物
/// </summary>
/// <param name="Input">元入力の相対pathと固定SHA-256</param>
internal sealed record InputConversionResult(SourceInput Input)
{
    /// <summary>
    /// 変換・JSON読取・参照素材照合の結果 開始前は未処理
    /// </summary>
    public ConversionStatus Status { get; init; } = ConversionStatus.Unprocessed;

    /// <summary>
    /// 変換器の終了値 未開始・起動失敗など観測できなかった場合はnull
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// 変換器から回収した標準出力を加工せず保持する文字列
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// 変換器から回収した標準エラーを加工せず保持する文字列
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// 実際の起動引数 配置rootを含む参考出典であり、論理引数と区別する
    /// </summary>
    public List<string> Arguments { get; init; } = [];

    /// <summary>
    /// この入力が所有する生成物 変換失敗時の部分生成物も保持する
    /// </summary>
    public List<Artifact> Artifacts { get; init; } = [];

    /// <summary>
    /// 変換・読取・素材照合の操作と対象を区別する入力別診断
    /// </summary>
    public List<CorpusDiagnostic> Diagnostics { get; init; } = [];
}
