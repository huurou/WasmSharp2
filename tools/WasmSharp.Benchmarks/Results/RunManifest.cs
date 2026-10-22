using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// runの予定・成否と保存した条件ファイルを結び付ける記録 完了時刻がないrunは完了を表さない。
/// </summary>
internal sealed record RunManifest(
    uint SchemaVersion,
    string RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    RunKind Kind,
    ResultStatus Status,
    List<string> PlannedCaseIds,
    List<CaseRunResult> Cases,
    ManifestFile? EnvironmentFile,
    ManifestFile? SourceFile,
    ManifestFile? AssetManifestFile,
    ManifestFile? CasesFile,
    ManifestFile? JobSettingsFile,
    List<RunFailure> Failures
);

/// <summary>
/// 性能比較、測定器の試走、原因調査、保持観測の用途
/// </summary>
internal enum RunKind
{
    Measurement,
    Smoke,
    Diagnostic,
    Retention,
}

/// <summary>
/// 成功、失敗、必須値の取得不能、未完了を区別する保存状態
/// </summary>
internal enum ResultStatus
{
    Succeeded,
    Failed,
    Unavailable,
    Incomplete,
}

/// <summary>
/// 1ケースの成否と採用した主指標 失敗時もBDNの記録と生データ参照を保持する。
/// </summary>
internal sealed record CaseRunResult(
    string CaseId,
    ResultStatus Status,
    CaseMetrics Metrics,
    BenchmarkReportResult? BenchmarkReport,
    List<RunFailure> Failures,
    List<ManifestFile> RawFiles
);

/// <summary>
/// BDN reportから取得したbuild・validationと各実行processの事実
/// </summary>
internal sealed record BenchmarkReportResult(
    bool BuildSucceeded,
    bool ValidationSucceeded,
    List<BenchmarkProcessResult> Processes
);

/// <summary>
/// BDNのExecuteResultに対応する終了値・Errorsと、同じprocessの検査ファイル
/// </summary>
internal sealed record BenchmarkProcessResult(
    string JobId,
    int? ProcessId,
    int? ExitCode,
    List<string> Errors,
    bool HasResultMeasurements,
    ManifestFile? CheckFile
);

/// <summary>
/// 推測で細分化せず、観測した失敗段階と最後に確認できた段階を保存する記録
/// </summary>
internal sealed record RunFailure(
    string? CaseId,
    RunStage Stage,
    RunStage? LastConfirmedStage,
    string Reason,
    string? ExceptionType,
    string? Message,
    string? InterruptionReason
);

/// <summary>
/// ツールが確認した処理段階 BDNから詳細が得られない終了はBenchmarkProcessExitとする。
/// </summary>
internal enum RunStage
{
    Preparation,
    Build,
    Validation,
    Precheck,
    ExecutionStarted,
    Postcheck,
    BenchmarkProcessExit,
    Saving,
    Completed,
}
