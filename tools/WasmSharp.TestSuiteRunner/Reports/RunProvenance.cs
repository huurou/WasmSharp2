namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// ケース同一性に含めない、実行時の環境と版の記録
/// </summary>
/// <param name="RunId">1回の実行を識別するID</param>
internal sealed record RunProvenance(string RunId)
{
    /// <summary>
    /// 実行を開始した日時
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// ランナーの版
    /// </summary>
    public string? RunnerVersion { get; init; }

    /// <summary>
    /// 実行に使用したランタイムの版
    /// </summary>
    public string? RuntimeVersion { get; init; }

    /// <summary>
    /// 実行環境のOS
    /// </summary>
    public string? OperatingSystem { get; init; }

    /// <summary>
    /// 実行環境のアーキテクチャ
    /// </summary>
    public string? Architecture { get; init; }

    /// <summary>
    /// CLIで解決したmanifestの絶対path
    /// </summary>
    public string? ManifestPath { get; init; }
}
