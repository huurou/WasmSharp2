using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class ComparisonFixture : IDisposable
{
    internal List<RunComparisonFixture> Baseline { get; } = [];
    internal List<RunComparisonFixture> Current { get; } = [];
    internal ComparisonPolicy Policy { get; set; }
    internal List<string> CaseIds { get; }

    internal ComparisonFixture(bool allCases = false)
    {
        var cases = AssetLoadingFixture.ReadCases();
        if (!allCases)
        {
            cases = [.. cases.Where(x => x.CaseId == "invoke.single")];
        }
        CaseIds = [.. cases.Select(x => x.CaseId)];
        Policy = new(
            1,
            "before-change",
            DateTimeOffset.Parse("2026-10-21T12:00:00Z"),
            [.. CaseIds
                .Select(x => new CaseComparisonPolicy(
                    x,
                    3,
                    Metric(null),
                    Metric(new AbsoluteThreshold(4))
                ))]
        );
        for (var index = 0; index < 3; index++)
        {
            var before = new RunComparisonFixture("before-" + index, "before");
            var after = new RunComparisonFixture("after-" + index, "after");
            before.SetCases(cases);
            after.SetCases(cases);
            Baseline.Add(before);
            Current.Add(after);
            SetValues(before, 101 - index, 8);
            SetValues(after, 100 - index, 4);
        }
    }

    internal static MetricPolicy Metric(MetricThreshold? target)
    {
        return new(
            new AbsoluteThreshold(5),
            target,
            new(new AbsoluteThreshold(2), new AbsoluteThreshold(2)),
            new AbsoluteThreshold(0.5),
            new AbsoluteThreshold(2)
        );
    }

    internal static void SetValues(
        RunComparisonFixture fixture,
        double time,
        double allocation,
        string? caseId = null
    )
    {
        fixture.Manifest = fixture.Manifest with
        {
            Cases = [.. fixture
                .Manifest.Cases.Select(x =>
                    caseId is not null && x.CaseId != caseId
                        ? x
                        : x with
                        {
                            Metrics = x.Metrics with
                            {
                                Time = x.Metrics.Time! with
                                {
                                    MeanNanosecondsPerOperation = time,
                                    MedianNanosecondsPerOperation = time + 50,
                                    ConfidenceInterval = new(time - 0.5, time + 0.5, 0.999),
                                },
                                Allocation = x.Metrics.Allocation! with
                                {
                                    TotalAllocatedBytes = (long)(allocation * 4096),
                                    BytesPerOperation = allocation,
                                    DisplayedBytesPerOperation = Math.Round(
                                        allocation,
                                        MidpointRounding.ToEven
                                    ),
                                },
                            },
                        }
                )],
        };
    }

    internal ComparisonRequest Save()
    {
        foreach (var run in Baseline.Concat(Current))
        {
            run.Save();
        }
        var path = Path.Combine(Baseline[0].DirectoryPath, "policy.json");
        var content = JsonSerializer.SerializeToUtf8Bytes(Policy, CaseJson.Options);
        File.WriteAllBytes(path, content);
        return new(
            [.. Baseline.Select(x => x.DirectoryPath)],
            [.. Current.Select(x => x.DirectoryPath)],
            path,
            Convert.ToHexStringLower(SHA256.HashData(content)),
            [.. CaseIds]
        );
    }

    public void Dispose()
    {
        foreach (var run in Baseline.Concat(Current))
        {
            run.Dispose();
        }
    }
}
