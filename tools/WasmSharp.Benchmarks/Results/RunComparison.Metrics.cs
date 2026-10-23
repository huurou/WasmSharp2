namespace WasmSharp.Benchmarks.Results;

internal static partial class RunComparison
{
    private static MetricComparison CompareMetric(
        List<RunMetricValue> baselineValues,
        List<RunMetricValue> currentValues,
        MetricPolicy? policy,
        int? minimumRuns,
        bool untargeted,
        List<ComparisonReason> prerequisites,
        string metric
    )
    {
        var baseline = Statistics(baselineValues);
        var current = Statistics(currentValues);
        MetricChange? change =
            baseline is null || current is null
                ? null
                : new(
                    current.Median - baseline.Median,
                    baseline.Median == 0 ? null : current.Median / baseline.Median
                );
        var reasons = prerequisites.ToList();
        if (baselineValues.Count < (minimumRuns ?? 3) || currentValues.Count < (minimumRuns ?? 3))
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.InsufficientMeasurements,
                    $"{metric}: 必要な独立run数={minimumRuns ?? 3}、有効な基準run={baselineValues.Count}、候補run={currentValues.Count}。"
                )
            );
        }
        var validPolicy =
            policy is not null && baseline is not null && IsValidPolicy(policy, baseline.Median);
        if (policy is not null && baseline is not null && !validPolicy)
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.MissingPolicy,
                    $"{metric}: 閾値は有限の非負値、削減目標は正値、同等増加幅は許容増加幅以下である必要があります。0基準には絶対差の閾値が必要です。"
                )
            );
        }
        if (validPolicy && current is not null)
        {
            foreach (var group in new[] { baseline!, current! })
            {
                var width = Width(policy!.RunVariation, group.Median);
                if (group.Runs.Any(x => Math.Abs(x.Value - group.Median) > width))
                {
                    reasons.Add(
                        new(
                            ComparisonReasonKind.ExcessiveVariation,
                            $"{metric}: run群 [{string.Join(", ", group.Runs.Select(x => x.RunId))}]が中央値からの反復間判断幅{width}を超えています。"
                        )
                    );
                }
            }
        }
        if (reasons.Any(x => IsBlocking(x.Kind)) || !validPolicy || change is null)
        {
            return new(
                baseline,
                current,
                change,
                ComparisonDecision.Indeterminate,
                null,
                false,
                reasons
            );
        }
        var difference = change.AbsoluteDifference;
        var allowed = difference <= Width(policy!.AllowedIncrease, baseline!.Median);
        if (!allowed)
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.AllowedIncreaseExceeded,
                    $"{metric}: 事前の許容増加幅を超えています。"
                )
            );
            return new(
                baseline,
                current,
                change,
                ComparisonDecision.Regressed,
                false,
                false,
                reasons
            );
        }
        var reduction = -difference;
        var beyondNoise = reduction > Width(policy.Noise, baseline.Median);
        var targetAchieved =
            policy.ReductionTarget is not null
            && reduction >= Width(policy.ReductionTarget, baseline.Median)
            && beyondNoise;
        if (targetAchieved)
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.ReductionTargetReached,
                    $"{metric}: 事前の削減目標を満たし、基準ノイズ幅を超えています。"
                )
            );
            return new(baseline, current, change, ComparisonDecision.Improved, true, true, reasons);
        }
        var equivalent =
            difference >= -Width(policy.Equivalence.Decrease, baseline.Median)
            && difference <= Width(policy.Equivalence.Increase, baseline.Median);
        if (
            untargeted
            && reduction > Width(policy.Equivalence.Decrease, baseline.Median)
            && beyondNoise
        )
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.ImprovementBeyondNoise,
                    $"{metric}: 非対象caseで同等範囲と基準ノイズ幅を超えて削減しています。"
                )
            );
            return new(
                baseline,
                current,
                change,
                ComparisonDecision.Improved,
                true,
                false,
                reasons
            );
        }
        if (policy.ReductionTarget is not null)
        {
            reasons.Add(
                new(
                    ComparisonReasonKind.TargetNotReached,
                    $"{metric}: 削減目標または基準ノイズ幅超過を満たしません。"
                )
            );
        }
        reasons.Add(
            equivalent
                ? new(ComparisonReasonKind.WithinEquivalence, $"{metric}: 事前の同等範囲内です。")
                : new(
                    ComparisonReasonKind.OutsideEquivalenceWithinAllowance,
                    $"{metric}: 同等範囲外ですが許容増加幅内です。"
                )
        );
        return new(
            baseline,
            current,
            change,
            equivalent ? ComparisonDecision.Equivalent : ComparisonDecision.Indeterminate,
            true,
            false,
            reasons
        );
    }

    private static RunGroupStatistics? Statistics(List<RunMetricValue> values)
    {
        if (values.Count == 0)
        {
            return null;
        }
        var ordered = values.Select(x => x.Value).Order().ToArray();
        var middle = ordered.Length / 2;
        var median =
            ordered.Length % 2 == 0
                ? ordered[middle - 1] + (ordered[middle] - ordered[middle - 1]) / 2
                : ordered[middle];
        return new(values, median, ordered[0], ordered[^1]);
    }

    private static bool IsValidPolicy(MetricPolicy policy, double baseline)
    {
        var thresholds = new[]
        {
            policy.AllowedIncrease,
            policy.Equivalence.Decrease,
            policy.Equivalence.Increase,
            policy.Noise,
        };
        return thresholds.All(x => IsValidThreshold(x, false))
            && IsValidThreshold(policy.RunVariation, false)
            && (policy.ReductionTarget is null || IsValidThreshold(policy.ReductionTarget, true))
            && (
                baseline != 0
                || thresholds.All(x => x is AbsoluteThreshold)
                    && policy.ReductionTarget is not RatioThreshold
            )
            && Width(policy.Equivalence.Increase, baseline)
                <= Width(policy.AllowedIncrease, baseline);
    }

    private static bool IsValidThreshold(MetricThreshold threshold, bool positive)
    {
        var value = threshold switch
        {
            AbsoluteThreshold x => x.Difference,
            RatioThreshold x => x.Fraction,
            _ => double.NaN,
        };
        return double.IsFinite(value) && (positive ? value > 0 : value >= 0);
    }

    private static double Width(MetricThreshold threshold, double value) =>
        threshold switch
        {
            AbsoluteThreshold x => x.Difference,
            RatioThreshold x => x.Fraction * value,
            _ => double.NaN,
        };
}
