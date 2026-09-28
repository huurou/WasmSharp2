using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 集計に用いるcommandの区分
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaseCategory>))]
internal enum CaseCategory
{
    /// <summary>
    /// 通常moduleとregister
    /// </summary>
    [JsonStringEnumMemberName("setup")]
    Setup,

    /// <summary>
    /// 単独action
    /// </summary>
    [JsonStringEnumMemberName("action")]
    Action,

    /// <summary>
    /// assert_returnと否定assertion
    /// </summary>
    [JsonStringEnumMemberName("assertion")]
    Assertion,
}
