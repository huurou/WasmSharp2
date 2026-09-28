using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// ケースの処理で実行したランナー上の段階
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaseStage>))]
internal enum CaseStage
{
    /// <summary>
    /// 公開Decode
    /// </summary>
    [JsonStringEnumMemberName("decode")]
    Decode,

    /// <summary>
    /// 公開Validate
    /// </summary>
    [JsonStringEnumMemberName("validate")]
    Validate,

    /// <summary>
    /// Instantiate前の公開import情報取得
    /// </summary>
    [JsonStringEnumMemberName("inspect_imports")]
    InspectImports,

    /// <summary>
    /// 公開Instantiate
    /// </summary>
    [JsonStringEnumMemberName("instantiate")]
    Instantiate,

    /// <summary>
    /// export一覧と実体の取得による登録
    /// </summary>
    [JsonStringEnumMemberName("register")]
    Register,

    /// <summary>
    /// invoke actionの公開呼び出し
    /// </summary>
    [JsonStringEnumMemberName("invoke")]
    Invoke,

    /// <summary>
    /// get actionの公開global取得
    /// </summary>
    [JsonStringEnumMemberName("get")]
    Get,
}
