using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 処理したcommandへ割り当てる6分類 未処理は分類に含めない
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaseOutcome>))]
internal enum CaseOutcome
{
    /// <summary>
    /// 期待した結果が成立した状態
    /// </summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>
    /// 判定した結果が期待と異なる状態
    /// </summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>
    /// ランタイムの未実装により判定を完了できない状態
    /// </summary>
    [JsonStringEnumMemberName("runtime_unsupported")]
    RuntimeUnsupported,

    /// <summary>
    /// 素材・JSON・ランナーの異常や公開契約外の失敗により判定を完了できない状態
    /// </summary>
    [JsonStringEnumMemberName("runner_error")]
    RunnerError,

    /// <summary>
    /// 実行対象外のtext module
    /// </summary>
    [JsonStringEnumMemberName("out_of_scope")]
    OutOfScope,

    /// <summary>
    /// 既知の失敗commandに依存するため実行しなかった状態
    /// </summary>
    [JsonStringEnumMemberName("blocked")]
    Blocked,
}
