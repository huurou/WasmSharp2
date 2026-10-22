using System.Text.Json.Serialization;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 変更前に固定する全ケースの判断基準 数値の決定と判定処理は持たない。
/// </summary>
internal sealed record ComparisonPolicy(
    uint SchemaVersion,
    string PolicyId,
    DateTimeOffset CreatedAtUtc,
    List<CaseComparisonPolicy> Cases
);

/// <summary>
/// 各ケースの両主指標と必要な独立run数
/// </summary>
internal sealed record CaseComparisonPolicy(
    string CaseId,
    int MinimumIndependentRuns,
    MetricPolicy Time,
    MetricPolicy Allocation
);

/// <summary>
/// 1指標の許容増加、削減目標、同等範囲、ノイズ幅と反復間判断幅 nullの目標は改善対象外を表す。
/// </summary>
internal sealed record MetricPolicy(
    MetricThreshold AllowedIncrease,
    MetricThreshold? ReductionTarget,
    EquivalenceRange Equivalence,
    MetricThreshold Noise,
    MetricThreshold RunVariation
);

/// <summary>
/// 同等とみなす減少側と増加側の幅 増加側を許容増加幅以下にする確認はpolicyの読込側が担う。
/// </summary>
internal sealed record EquivalenceRange(MetricThreshold Decrease, MetricThreshold Increase);

/// <summary>
/// 絶対差と比率を混同しない数値基準 保存時にもkindで区別する。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(AbsoluteThreshold), "absolute")]
[JsonDerivedType(typeof(RatioThreshold), "ratio")]
internal abstract record MetricThreshold
{
    private protected MetricThreshold() { }
}

/// <summary>
/// 時間ではns/op、割り当てではbytes/opの絶対差の大きさ
/// </summary>
internal sealed record AbsoluteThreshold(double Difference) : MetricThreshold;

/// <summary>
/// 基準値に対する変化幅の比率 0.05は5%の増加または削減を表す。
/// </summary>
internal sealed record RatioThreshold(double Fraction) : MetricThreshold;
