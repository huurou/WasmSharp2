using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 同じケースに対応する時間・割り当て・GC 取得不能はnullと理由で保持し、0で補完しない。
/// </summary>
internal sealed record CaseMetrics(
    TimeStatistics? Time,
    string? TimeUnavailableReason,
    AllocationStatistics? Allocation,
    string? AllocationUnavailableReason,
    GcStatistics? Gc,
    string? GcUnavailableReason
)
{
    public string? TimeUnavailableReason { get; init; } =
        RequireReason(Time, TimeUnavailableReason, nameof(TimeUnavailableReason));

    public string? AllocationUnavailableReason { get; init; } =
        RequireReason(Allocation, AllocationUnavailableReason, nameof(AllocationUnavailableReason));

    public string? GcUnavailableReason { get; init; } =
        RequireReason(Gc, GcUnavailableReason, nameof(GcUnavailableReason));

    private static string? RequireReason(object? metric, string? reason, string parameterName)
    {
        if (metric is null && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("指標を取得できない場合は理由が必要です。", parameterName);
        }

        return reason;
    }
}

/// <summary>
/// BDNのWorkload/Resultに対応するバッチ平均の統計 単位はns/opで、個々のInvokeの遅延分布ではない。
/// </summary>
/// <param name="RawMeasurements">Actual・Resultのlaunch・iteration・mode/stage・操作数・経過nsを保持するCSV</param>
internal sealed record TimeStatistics(
    int MeasurementCount,
    double MeanNanosecondsPerOperation,
    double MedianNanosecondsPerOperation,
    double StandardDeviationNanosecondsPerOperation,
    ConfidenceInterval ConfidenceInterval,
    ManifestFile RawMeasurements
);

/// <summary>
/// BDNが計算した各runの信頼区間 Levelは0.999が99.9%を表し、独立run間の差の区間とは区別する。
/// </summary>
internal sealed record ConfidenceInterval(double Lower, double Upper, double Level);

/// <summary>
/// process全体のmanaged累積割り当ての観測 保持量・nativeメモリ・guest線形メモリの使用量は含まない。
/// </summary>
/// <param name="TotalAllocatedBytes">GcStats.GetTotalAllocatedBytes(false)の値</param>
/// <param name="BytesPerOperation">総bytesを操作数で割った丸め前の値</param>
internal sealed record AllocationStatistics(
    long TotalAllocatedBytes,
    long TotalOperations,
    double BytesPerOperation,
    double DisplayedBytesPerOperation,
    AllocationRounding Rounding,
    AllocationScope Scope,
    ObservationIteration Iteration
);

/// <summary>
/// 表示値を得る丸め条件 表示0を割り当て不存在と解釈しない。
/// </summary>
internal sealed record AllocationRounding(
    int DecimalPlaces,
    MidpointRounding MidpointRounding,
    string Description
);

/// <summary>
/// 固定版BDN/.NET10で取得する割り当ての観測範囲
/// </summary>
internal enum AllocationScope
{
    ProcessManagedAllocations,
}

/// <summary>
/// 割り当てとGCは時間反復とは別の追加反復で観測する。
/// </summary>
internal enum ObservationIteration
{
    SeparateFromTiming,
}

/// <summary>
/// 割り当てと同じ観測区間のGC総回数と1,000操作当たりの値
/// </summary>
internal sealed record GcStatistics(
    long TotalOperations,
    GcGenerationStatistics Gen0,
    GcGenerationStatistics Gen1,
    GcGenerationStatistics Gen2,
    ObservationIteration Iteration
);

/// <summary>
/// 1つのgenerationの総回数と操作数による正規化値
/// </summary>
internal sealed record GcGenerationStatistics(
    long TotalCollections,
    double CollectionsPerThousandOperations
);
