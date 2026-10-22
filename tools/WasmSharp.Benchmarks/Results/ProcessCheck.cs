namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 同じrun・case・Job・processの計時外検査 子processが読み込んだassemblyを記録する。
/// </summary>
internal sealed record ProcessCheck(
    string RunId,
    string CaseId,
    string JobId,
    int ProcessId,
    CorrectnessCheck? Precheck,
    bool ExecutionStarted,
    CorrectnessCheck? Postcheck,
    RunStage? LastConfirmedStage,
    List<AssemblyIdentity> LoadedAssemblies,
    List<RunFailure> Failures
);

/// <summary>
/// 前検査または後検査の成立状態と、固定期待値に対する観測
/// </summary>
internal sealed record CorrectnessCheck(ResultStatus Status, List<CheckObservation> Observations);

/// <summary>
/// 検査で比較した期待値と観測値の表示記録 固定期待値の定義そのものはケースに置く。
/// </summary>
internal sealed record CheckObservation(string Name, string Expected, string Actual, bool Matches);
