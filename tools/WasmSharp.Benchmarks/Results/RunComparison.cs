using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 保存済みrunの条件を照合する 数値と事前policyによる判定は別の工程で扱う。
/// </summary>
internal static partial class RunComparison
{
    /// <summary>
    /// 保存済みの2runについて、指定caseの条件差と比較できない理由を返す
    /// </summary>
    internal static RunConditionComparison CompareConditions(
        string baselineDirectory,
        string currentDirectory,
        string caseId
    )
    {
        var reasons = new List<ComparisonReason>();
        var baseline = ReadConditions(baselineDirectory, caseId, reasons);
        var current = ReadConditions(currentDirectory, caseId, reasons);
        return CompareConditions(baseline, current, reasons);
    }

    private static RunConditionComparison CompareConditions(
        RunConditions? baseline,
        RunConditions? current,
        List<ComparisonReason> reasons
    )
    {
        var differences = new List<ConditionDifference>();
        if (baseline is not null && current is not null)
        {
            foreach (
                var name in baseline
                    .Values.Keys.Union(current.Values.Keys)
                    .Order(StringComparer.Ordinal)
            )
            {
                var before = baseline.Values.GetValueOrDefault(name);
                var after = current.Values.GetValueOrDefault(name);
                if (before != after)
                {
                    differences.Add(new(name, before, after, baseline.RunId, current.RunId));
                }
            }
        }

        if (differences.Count != 0)
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.ConditionMismatch,
                    "ランタイムコード以外の比較条件が異なります。"
                )
            );
        }
        return new(differences, reasons);
    }

    private static RunConditions? ReadConditions(
        string directory,
        string caseId,
        List<ComparisonReason> reasons,
        RunManifest? savedRun = null
    )
    {
        try
        {
            // 保存参照の存在とhashはRunStoreに集約し、観測された外部pathを再読込しない
            var run = savedRun ?? RunStore.Load(directory);
            var result = run.Cases.SingleOrDefault(x => x.CaseId == caseId);
            if (
                run.Kind != RunKind.Measurement
                || run.Status != ResultStatus.Succeeded
                || result?.Status != ResultStatus.Succeeded
            )
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.InsufficientMeasurements,
                        $"run '{run.RunId}': kind={run.Kind}, status={run.Status}, case '{caseId}' status={result?.Status.ToString() ?? "Missing"}。成功した通常測定が必要です。"
                    )
                );
                return null;
            }
            var source = Read<RunSource>(directory, run.SourceFile!);
            if (source.Changes.Count != 0 || !AssembliesUnchanged(source))
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.ConditionMismatch,
                        $"run '{run.RunId}': 使用source/project/assemblyの途中変更があります。changes={JsonSerializer.Serialize(source.Changes, CaseJson.Options)}"
                    )
                );
                return null;
            }
            if (run.Failures.Count != 0 || result.Failures.Count != 0)
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.CorrectnessFailure,
                        $"run '{run.RunId}': 保存された失敗があります。"
                    )
                );
                return null;
            }
            var definition = Read<List<BenchmarkCase>>(directory, run.CasesFile!)
                .Single(x => x.CaseId == caseId);
            var jobs = Read<List<ProcessConfiguration>>(directory, run.JobSettingsFile!);
            var checks = ReadAffinityChecks(directory, run, jobs);
            return RunConditions.Create(
                run,
                definition,
                Read<AssetManifest>(directory, run.AssetManifestFile!),
                Read<RunEnvironment>(directory, run.EnvironmentFile!),
                source,
                jobs,
                checks
            );
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or JsonException
                        or InvalidOperationException
                        or ArgumentException
                        or UnauthorizedAccessException
            )
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.ConditionMismatch,
                    $"保存run '{directory}'の比較条件を読めません: {exception.Message}"
                )
            );
            return null;
        }
    }

    private static List<ProcessCheck> ReadAffinityChecks(
        string directory,
        RunManifest run,
        List<ProcessConfiguration> jobs
    )
    {
        var checks = new List<ProcessCheck>();
        foreach (var result in run.Cases)
        {
            var processes = result.BenchmarkReport?.Processes ?? [];
            if (
                jobs.Count == 0
                || jobs.Any(x => processes.Count(y => y.JobId == x.JobId) < x.LaunchCount)
            )
            {
                throw new InvalidDataException("全予定子processのaffinity観測が揃っていません。");
            }
            foreach (var process in processes)
            {
                if (process.CheckFile is null || !jobs.Any(x => x.JobId == process.JobId))
                {
                    throw new InvalidDataException("子processのaffinity検査がありません。");
                }
                var check = Read<ProcessCheck>(directory, process.CheckFile);
                if (
                    check.AffinityBefore is null or 0
                    || check.AffinityAfter is null or 0
                    || check.AffinityBefore != check.AffinityAfter
                    || check.AffinityUnavailableReason is not null
                )
                {
                    throw new InvalidDataException(
                        $"case '{result.CaseId}', PID={process.ProcessId}: affinityの前後観測が欠落または不一致です。{check.AffinityUnavailableReason}"
                    );
                }
                checks.Add(check);
            }
        }
        if (
            checks
                .GroupBy(x => x.JobId)
                .Any(x => x.Select(y => y.AffinityBefore).Distinct().Count() != 1)
        )
        {
            throw new InvalidDataException("同じJobの子process間でaffinityが異なります。");
        }
        return checks;
    }

    private static bool AssembliesUnchanged(RunSource source) =>
        source.AssembliesBefore.Count == source.AssembliesAfter.Count
        && source.AssembliesBefore.All(x =>
            source.AssembliesAfter.Any(y =>
                y.Role == x.Role && y.Path == x.Path && y.Sha256 == x.Sha256
            )
        );

    private static T Read<T>(string directory, ManifestFile file) =>
        JsonSerializer.Deserialize<T>(
            File.ReadAllBytes(Path.Combine(directory, file.RelativePath)),
            CaseJson.Options
        )!;
}

/// <summary>
/// 条件差と比較の前提不成立 数値の改善・悪化を表す判定とは分ける。
/// </summary>
internal sealed record RunConditionComparison(
    List<ConditionDifference> Differences,
    List<ComparisonReason> Reasons
)
{
    internal bool IsComparable => Differences.Count == 0 && Reasons.Count == 0;
}
