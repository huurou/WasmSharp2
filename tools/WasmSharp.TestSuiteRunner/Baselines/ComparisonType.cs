using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// 比較の種類
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ComparisonType>))]
internal enum ComparisonType
{
    /// <summary>
    /// 2つのmanifestによる変換結果の再現性比較
    /// </summary>
    [JsonStringEnumMemberName("conversion")]
    Conversion,

    /// <summary>
    /// 2つのRunReportによるケース単位の回帰比較
    /// </summary>
    [JsonStringEnumMemberName("run")]
    Run,
}
