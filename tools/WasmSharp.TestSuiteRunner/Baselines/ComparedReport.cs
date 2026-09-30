namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 比較に使用した保存済み結果の識別
/// </summary>
/// <param name="Path">CLIで解決した結果JSONの絶対path</param>
internal sealed record ComparedReport(string Path)
{
    /// <summary>
    /// 実行結果の実行ID manifestではnull
    /// </summary>
    public string? RunId { get; init; }
}
