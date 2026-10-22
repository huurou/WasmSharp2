namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 固定期待値との照合結果 失敗時はケース・処理段階・対象・期待・観測を保持する。
/// </summary>
internal sealed record VerificationResult(
    bool Succeeded,
    string CaseId,
    string Stage,
    string? Subject,
    string? Expected,
    string? Observed
);
