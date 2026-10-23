using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

internal static partial class RunComparison
{
    /// <summary>
    /// 保存runディレクトリ群と指定hashの事前policyを読み、各caseの両指標を判定する
    /// </summary>
    internal static ComparisonReport Compare(ComparisonRequest request)
    {
        var reasons = new List<ComparisonReason>();
        var baseline = ReadRuns(request.BaselineRunDirectories, reasons);
        var current = ReadRuns(request.CurrentRunDirectories, reasons);
        var policy = ReadPolicy(request, reasons);
        if (
            baseline
                .Select(x => x.Manifest.RunId)
                .Intersect(current.Select(x => x.Manifest.RunId))
                .Any()
        )
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.InsufficientMeasurements,
                    "同じrun IDを基準群と候補群に重複使用できません。"
                )
            );
        }
        return new(
            1,
            baseline.Select(x => x.Reference).ToList(),
            current.Select(x => x.Reference).ToList(),
            new(request.PolicyPath, request.PolicySha256),
            request
                .CaseIds.Distinct(StringComparer.Ordinal)
                .Select(x => CompareCase(x, baseline, current, policy, reasons))
                .ToList()
        );
    }

    private static List<ComparisonRun> ReadRuns(
        List<string> directories,
        List<ComparisonReason> reasons
    )
    {
        var runs = new List<ComparisonRun>();
        foreach (var directory in directories)
        {
            try
            {
                var manifest = RunStore.Load(directory);
                var path = Path.Combine(directory, "run.json");
                runs.Add(
                    new(
                        directory,
                        manifest,
                        new(
                            manifest.RunId,
                            new(
                                path,
                                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
                            )
                        )
                    )
                );
            }
            catch (Exception exception) when (IsReadFailure(exception))
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.InsufficientMeasurements,
                        $"保存run '{directory}'を読めません: {exception.Message}"
                    )
                );
            }
        }
        if (
            runs.Select(x => x.Manifest.RunId).Distinct(StringComparer.Ordinal).Count()
            != runs.Count
        )
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.InsufficientMeasurements,
                    "群内の独立run IDが重複しています。"
                )
            );
        }
        return runs;
    }

    private static ComparisonPolicy? ReadPolicy(
        ComparisonRequest request,
        List<ComparisonReason> reasons
    )
    {
        try
        {
            var content = File.ReadAllBytes(request.PolicyPath);
            var hash = Convert.ToHexStringLower(SHA256.HashData(content));
            if (hash != request.PolicySha256)
            {
                throw new InvalidDataException(
                    $"policyのSHA-256が一致しません。指定={request.PolicySha256}, 保存内容={hash}"
                );
            }
            var policy = JsonSerializer.Deserialize<ComparisonPolicy>(content, CaseJson.Options)!;
            if (
                policy is null
                || policy.SchemaVersion != 1
                || string.IsNullOrWhiteSpace(policy.PolicyId)
            )
            {
                throw new InvalidDataException("policyの版または識別子が不正です。");
            }
            return policy;
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.MissingPolicy,
                    $"事前policy '{request.PolicyPath}'を使用できません: {exception.Message}"
                )
            );
            return null;
        }
    }

    private static CaseComparison CompareCase(
        string caseId,
        List<ComparisonRun> baseline,
        List<ComparisonRun> current,
        ComparisonPolicy? policy,
        List<ComparisonReason> inputReasons
    )
    {
        var reasons = inputReasons.ToList();
        var differences = new List<ConditionDifference>();
        RunConditions? reference = null;
        foreach (var run in baseline.Concat(current))
        {
            var result = run.Manifest.Cases.FirstOrDefault(x => x.CaseId == caseId);
            if (run.Manifest.Status == ResultStatus.Failed || result?.Status == ResultStatus.Failed)
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.CorrectnessFailure,
                        $"run '{run.Manifest.RunId}'のcase '{caseId}'は成功していません。"
                    )
                );
            }
            var conditions = ReadConditions(run.Directory, caseId, reasons, run.Manifest);
            if (conditions is not null)
            {
                if (reference is null)
                {
                    reference = conditions;
                }
                else
                {
                    var comparison = CompareConditions(reference, conditions, []);
                    differences.AddRange(comparison.Differences);
                    reasons.AddRange(comparison.Reasons);
                }
            }
            if (result?.Metrics.Gc is null)
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.InsufficientMeasurements,
                        $"run '{run.Manifest.RunId}'のcase '{caseId}'にGC観測がありません: {result?.Metrics.GcUnavailableReason}"
                    )
                );
            }
            if (
                result is not null
                && (result.Metrics.Time is null || result.Metrics.Allocation is null)
            )
            {
                reasons.Add(
                    new(
                        ComparisonReasonKind.InsufficientMeasurements,
                        $"run '{run.Manifest.RunId}'のcase '{caseId}'に必須指標がありません。時間={result.Metrics.TimeUnavailableReason}、割り当て={result.Metrics.AllocationUnavailableReason}"
                    )
                );
            }
        }
        var policies = policy?.Cases.Where(x => x.CaseId == caseId).ToList() ?? [];
        var casePolicy = policies.Count == 1 ? policies[0] : null;
        if (casePolicy is null || casePolicy.MinimumIndependentRuns < 3)
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.MissingPolicy,
                    $"case '{caseId}'の一意なpolicyと3以上の必要独立run数が必要です。"
                )
            );
            casePolicy = null;
        }
        var untargeted =
            casePolicy?.Time.ReductionTarget is null
            && casePolicy?.Allocation.ReductionTarget is null;
        var time = CompareMetric(
            MetricValues(baseline, caseId, true),
            MetricValues(current, caseId, true),
            casePolicy?.Time,
            casePolicy?.MinimumIndependentRuns,
            untargeted,
            reasons,
            "時間"
        );
        var allocation = CompareMetric(
            MetricValues(baseline, caseId, false),
            MetricValues(current, caseId, false),
            casePolicy?.Allocation,
            casePolicy?.MinimumIndependentRuns,
            untargeted,
            reasons,
            "割り当て"
        );
        reasons.AddRange(time.Reasons);
        reasons.AddRange(allocation.Reasons);
        reasons = reasons.Distinct().ToList();
        var blocked = reasons.Any(x => IsBlocking(x.Kind));
        var decision =
            blocked ? ComparisonDecision.Indeterminate
            : time.Decision == ComparisonDecision.Regressed
            || allocation.Decision == ComparisonDecision.Regressed
                ? ComparisonDecision.Regressed
            : time.TargetAchieved
            || allocation.TargetAchieved
            || (
                untargeted
                && (
                    time.Decision == ComparisonDecision.Improved
                    || allocation.Decision == ComparisonDecision.Improved
                )
            )
                ? ComparisonDecision.Improved
            : time.Decision == ComparisonDecision.Equivalent
            && allocation.Decision == ComparisonDecision.Equivalent
                ? ComparisonDecision.Equivalent
            : ComparisonDecision.Indeterminate;
        return new(
            caseId,
            decision,
            decision == ComparisonDecision.Improved
                && (time.TargetAchieved || allocation.TargetAchieved),
            blocked || decision == ComparisonDecision.Regressed,
            reasons,
            differences,
            time,
            allocation
        );
    }

    private static List<RunMetricValue> MetricValues(
        List<ComparisonRun> runs,
        string caseId,
        bool time
    )
    {
        var values = new List<RunMetricValue>();
        foreach (var run in runs)
        {
            var result = run.Manifest.Cases.FirstOrDefault(x => x.CaseId == caseId);
            if (
                run.Manifest.Kind != RunKind.Measurement
                || run.Manifest.Status != ResultStatus.Succeeded
                || result?.Status != ResultStatus.Succeeded
                || result.Failures.Count != 0
                || run.Manifest.Failures.Count != 0
            )
            {
                continue;
            }
            if (
                time
                && result.Metrics.Time is { } timing
                && timing.MeasurementCount > 0
                && double.IsFinite(timing.MeanNanosecondsPerOperation)
                && timing.MeanNanosecondsPerOperation >= 0
                && double.IsFinite(timing.ConfidenceInterval.Lower)
                && double.IsFinite(timing.ConfidenceInterval.Upper)
                && timing.ConfidenceInterval.Lower <= timing.ConfidenceInterval.Upper
                && timing.ConfidenceInterval.Level is > 0 and <= 1
            )
            {
                values.Add(
                    new(
                        run.Manifest.RunId,
                        timing.MeanNanosecondsPerOperation,
                        timing.ConfidenceInterval
                    )
                );
            }
            else if (
                !time
                && result.Metrics.Allocation is { } allocation
                && allocation.TotalOperations > 0
                && allocation.TotalAllocatedBytes >= 0
                && double.IsFinite(allocation.BytesPerOperation)
                && allocation.BytesPerOperation >= 0
            )
            {
                values.Add(new(run.Manifest.RunId, allocation.BytesPerOperation, null));
            }
        }
        return values;
    }

    private static bool IsBlocking(ComparisonReasonKind kind) =>
        kind
            is ComparisonReasonKind.ConditionMismatch
                or ComparisonReasonKind.CorrectnessFailure
                or ComparisonReasonKind.InsufficientMeasurements
                or ComparisonReasonKind.ExcessiveVariation
                or ComparisonReasonKind.MissingPolicy;

    private static bool IsReadFailure(Exception exception) =>
        exception
            is IOException
                or InvalidDataException
                or JsonException
                or InvalidOperationException
                or ArgumentException
                or UnauthorizedAccessException;

    private sealed record ComparisonRun(
        string Directory,
        RunManifest Manifest,
        ComparedRun Reference
    );
}
