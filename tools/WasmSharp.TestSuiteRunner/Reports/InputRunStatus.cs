using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 入力単位の処理状態
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InputRunStatus>))]
internal enum InputRunStatus
{
    /// <summary>
    /// 処理を開始していない状態
    /// </summary>
    [JsonStringEnumMemberName("unprocessed")]
    Unprocessed,

    /// <summary>
    /// 処理を開始したが、中断により列挙済みの全commandを記録できていない状態
    /// </summary>
    [JsonStringEnumMemberName("incomplete")]
    Incomplete,

    /// <summary>
    /// 入力異常を含めて、列挙済みの全commandを記録した状態
    /// </summary>
    [JsonStringEnumMemberName("processed")]
    Processed,
}
