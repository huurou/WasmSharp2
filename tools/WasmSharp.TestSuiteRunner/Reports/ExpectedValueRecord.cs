using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// WABTの文字列を加工せず保持する期待値
/// </summary>
/// <param name="Type">WABTの値型名</param>
internal sealed record ExpectedValueRecord(string Type)
{
    /// <summary>
    /// scalar・参照の値、またはNaN pattern v128と型だけの結果宣言ではnull
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// v128のlane型 v128以外ではnull
    /// </summary>
    public string? LaneType { get; init; }

    /// <summary>
    /// v128のlane0から順の値またはNaN pattern
    /// </summary>
    public List<string> Lanes { get; init; } = [];

    /// <summary>
    /// Core 3.0のeitherでこの結果位置に許容される候補を指定順に保持する。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ExpectedValueRecord>? Values { get; init; }

    /// <summary>
    /// 比較結果に含めるレーンと候補のリストを複製する。
    /// </summary>
    internal ExpectedValueRecord CreateSnapshot()
    {
        return this with { Lanes = [.. Lanes], Values = Values?.Select(x => x.CreateSnapshot()).ToList() };
    }
}
