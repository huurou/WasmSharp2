using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Baselines;

/// <summary>
/// ケースの前後の変化の種類
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaseChange>))]
internal enum CaseChange
{
    /// <summary>
    /// 分類・期待・実際・診断が変わらない状態
    /// </summary>
    [JsonStringEnumMemberName("unchanged")]
    Unchanged,

    /// <summary>
    /// 分類・期待・実際・診断のいずれかが変わった状態
    /// </summary>
    [JsonStringEnumMemberName("changed")]
    Changed,

    /// <summary>
    /// 現結果にだけ存在する状態
    /// </summary>
    [JsonStringEnumMemberName("added")]
    Added,

    /// <summary>
    /// 比較元にだけ存在する状態
    /// </summary>
    [JsonStringEnumMemberName("missing")]
    Missing,
}
