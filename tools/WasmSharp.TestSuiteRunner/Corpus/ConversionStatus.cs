using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// commandの分類と区別する入力単位の変換状態
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConversionStatus>))]
internal enum ConversionStatus
{
    /// <summary>
    /// 変換を開始していない状態
    /// </summary>
    [JsonStringEnumMemberName("unprocessed")]
    Unprocessed,

    /// <summary>
    /// 変換・JSON読取・参照素材の照合が成功した状態
    /// </summary>
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,

    /// <summary>
    /// 変換・読取・照合のいずれかが失敗した状態
    /// </summary>
    [JsonStringEnumMemberName("runner_error")]
    RunnerError,
}
