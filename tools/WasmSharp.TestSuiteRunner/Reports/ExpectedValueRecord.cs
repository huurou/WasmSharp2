namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// WABTの文字列を加工せず保持する期待値
/// </summary>
/// <param name="Type">WABTの値型名</param>
internal sealed record ExpectedValueRecord(string Type)
{
    /// <summary>
    /// scalar・参照の値、またはNaN pattern。v128と型だけの結果宣言ではnull
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// v128のlane型。v128以外ではnull
    /// </summary>
    public string? LaneType { get; init; }

    /// <summary>
    /// v128のlane0から順の値またはNaN pattern
    /// </summary>
    public List<string> Lanes { get; init; } = [];
}
