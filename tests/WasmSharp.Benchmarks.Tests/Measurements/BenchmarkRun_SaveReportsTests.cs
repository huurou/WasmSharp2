using System.Globalization;
using System.Security.Cryptography;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_SaveReportsTests
{
    [Test]
    public async Task Resultが1反復だけのSmoke_時間は理由付き取得不能でも割り当てとrawを残す()
    {
        // Arrange
        // タスク5.3の正常Smokeから取り出したActual・Result・GC
        using var fixture = new BenchmarkReportFixture([
            "WorkloadActual 1: 64 op, 43400.00 ns",
            "WorkloadResult 1: 64 op, 43100.00 ns",
            "// GC: 0 0 0 24576 64",
        ]);

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Metrics.Time).IsNull();
            await Assert.That(result.Metrics.TimeUnavailableReason).Contains("99.9%信頼区間");
            await Assert.That(result.Metrics.Allocation!.BytesPerOperation).IsEqualTo(384);
            await Assert.That(result.Metrics.Gc!.TotalOperations).IsEqualTo(64);
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(result.RawFiles.Count).IsEqualTo(2);
            await Assert
                .That(result.RawFiles.Any(x => x.RelativePath.EndsWith("-measurements.csv")))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("// GC: 0 0 0 0 0")]
    public async Task GCの観測または操作数がない_割り当てとGCを0で補完せず理由を残す(string? gcLine)
    {
        // Arrange
        var lines = BenchmarkReportFixture.ReadLines().Where(x => !x.StartsWith("// GC:")).ToList();
        if (gcLine is not null)
        {
            lines.Add(gcLine);
        }
        using var fixture = new BenchmarkReportFixture(lines);

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Metrics.Time).IsNotNull();
            await Assert.That(result.Metrics.Allocation).IsNull();
            await Assert.That(result.Metrics.AllocationUnavailableReason).Contains("GC観測操作数");
            await Assert.That(result.Metrics.Gc).IsNull();
            await Assert.That(result.Metrics.GcUnavailableReason).Contains("GC観測操作数");
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
        }
    }

    [Test]
    [Arguments(
        "// GC: 179 0 0 2818572288 8388608",
        2818572288L,
        8388608L,
        336.0,
        336.0,
        179L,
        0L,
        0L,
        0.021338462829589844,
        0.0,
        0.0
    )]
    [Arguments("// GC: 2 1 1 5 20", 5L, 20L, 0.25, 0.0, 2L, 1L, 1L, 100.0, 50.0, 50.0)]
    [Arguments("// GC: 1 2 3 5 2", 5L, 2L, 2.5, 2.0, 1L, 2L, 3L, 500.0, 1000.0, 1500.0)]
    public async Task 割り当てとGCを保存する_総量と丸め前後のbytesと別反復の観測範囲が残る(
        string gcLine,
        long totalBytes,
        long operations,
        double unrounded,
        double displayed,
        long gen0,
        long gen1,
        long gen2,
        double normalized0,
        double normalized1,
        double normalized2
    )
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            BenchmarkReportFixture.ReadLines().Where(x => !x.StartsWith("// GC:")).Append(gcLine)
        );

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        await Assert.That(result.Metrics.Allocation).IsNotNull();
        await Assert.That(result.Metrics.Gc).IsNotNull();
        await Assert.That(result.Metrics.Time).IsNotNull();
        var allocation = result.Metrics.Allocation!;
        var gc = result.Metrics.Gc!;
        using (Assert.Multiple())
        {
            await Assert.That(allocation.TotalAllocatedBytes).IsEqualTo(totalBytes);
            await Assert.That(allocation.TotalOperations).IsEqualTo(operations);
            await Assert.That(allocation.BytesPerOperation).IsEqualTo(unrounded);
            await Assert.That(allocation.DisplayedBytesPerOperation).IsEqualTo(displayed);
            await Assert
                .That(allocation.Scope)
                .IsEqualTo(AllocationScope.ProcessManagedAllocations);
            await Assert
                .That(allocation.Iteration)
                .IsEqualTo(ObservationIteration.SeparateFromTiming);
            await Assert.That(allocation.Rounding.DecimalPlaces).IsEqualTo(0);
            await Assert
                .That(allocation.Rounding.MidpointRounding)
                .IsEqualTo(MidpointRounding.ToEven);
            await Assert
                .That(allocation.Rounding.Description)
                .Contains("表示0は割り当て不存在を意味しない");
            await Assert.That(result.Metrics.AllocationUnavailableReason).IsNull();
            await Assert.That(result.Metrics.GcUnavailableReason).IsNull();
            await Assert.That(gc.TotalOperations).IsEqualTo(operations);
            await Assert.That(gc.Gen0.TotalCollections).IsEqualTo(gen0);
            await Assert.That(gc.Gen1.TotalCollections).IsEqualTo(gen1);
            await Assert.That(gc.Gen2.TotalCollections).IsEqualTo(gen2);
            await Assert.That(gc.Gen0.CollectionsPerThousandOperations).IsEqualTo(normalized0);
            await Assert.That(gc.Gen1.CollectionsPerThousandOperations).IsEqualTo(normalized1);
            await Assert.That(gc.Gen2.CollectionsPerThousandOperations).IsEqualTo(normalized2);
            await Assert.That(gc.Iteration).IsEqualTo(ObservationIteration.SeparateFromTiming);
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert
                .That(result.RawFiles.Any(x => x.RelativePath == "raw/bdn-output.txt"))
                .IsTrue();
            await Assert
                .That(
                    File.ReadAllText(Path.Combine(fixture.DirectoryPath, "raw", "bdn-output.txt"))
                )
                .Contains(gcLine);
        }
    }

    [Test]
    public async Task 固定reportを保存する_Resultの時間統計とActualを含むrawが同じcaseへ残る()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);
        var result = manifest.Cases.Single();

        // Assert
        await Assert.That(result.Metrics.Time).IsNotNull();
        var time = result.Metrics.Time!;
        var raw = File.ReadAllBytes(
            Path.Combine(fixture.DirectoryPath, time.RawMeasurements.RelativePath)
        );
        var rows = System
            .Text.Encoding.UTF8.GetString(raw)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var values = rows.Skip(1).Select(x => x.TrimEnd('\r').Split(',')).ToArray();
        var results = values.Where(x => x[2] == "Workload" && x[3] == "Result").ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(result.CaseId).IsEqualTo("invoke.single");
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(time.MeasurementCount).IsEqualTo(15);
            await Assert.That(time.MeanNanosecondsPerOperation).IsEqualTo(76.04321956634521);
            await Assert.That(time.MedianNanosecondsPerOperation).IsEqualTo(75.50182342529297);
            await Assert
                .That(time.StandardDeviationNanosecondsPerOperation)
                .IsEqualTo(1.3306110771777684);
            await Assert.That(time.ConfidenceInterval.Lower).IsEqualTo(74.6207156172511);
            await Assert.That(time.ConfidenceInterval.Upper).IsEqualTo(77.46572351543934);
            await Assert.That(time.ConfidenceInterval.Level).IsEqualTo(0.999);
            await Assert.That(result.Metrics.TimeUnavailableReason).IsNull();
            await Assert
                .That(rows[0])
                .IsEqualTo(
                    "launch_index,iteration_index,iteration_mode,iteration_stage,operations,elapsed_nanoseconds"
                );
            await Assert.That(values.Length).IsEqualTo(88);
            await Assert
                .That(values.Count(x => x[2] == "Workload" && x[3] == "Actual"))
                .IsEqualTo(15);
            await Assert.That(results.Length).IsEqualTo(15);
            await Assert.That(results[0][0]).IsEqualTo("1");
            await Assert.That(results[0][1]).IsEqualTo("1");
            await Assert.That(results[0][4]).IsEqualTo("8388608");
            await Assert
                .That(
                    results.Average(x =>
                        double.Parse(x[5], CultureInfo.InvariantCulture)
                        / long.Parse(x[4], CultureInfo.InvariantCulture)
                    )
                )
                .IsEqualTo(76.04321956634521);
            await Assert
                .That(time.RawMeasurements.Sha256)
                .IsEqualTo(Convert.ToHexStringLower(SHA256.HashData(raw)));
            await Assert.That(result.RawFiles.Contains(time.RawMeasurements)).IsTrue();
        }
    }
}
