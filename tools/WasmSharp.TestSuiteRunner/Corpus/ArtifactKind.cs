using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// manifestが同定する生成物の種類
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArtifactKind>))]
internal enum ArtifactKind
{
    /// <summary>
    /// commandと素材参照を持つJSON
    /// </summary>
    [JsonStringEnumMemberName("json")]
    Json,

    /// <summary>
    /// 実行に使用するbinary module
    /// </summary>
    [JsonStringEnumMemberName("wasm")]
    Wasm,

    /// <summary>
    /// 素材照合の対象に含め、実行対象外として扱うtext module
    /// </summary>
    [JsonStringEnumMemberName("wat")]
    Wat,
}
