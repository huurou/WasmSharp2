using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using Perfolizer.Mathematics.OutlierDetection;
using WasmSharp.Benchmarks.Measurements;
using RunMode = BenchmarkDotNet.Jobs.RunMode;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkConfig_CreateTests
{
    [Test]
    public async Task 本測定設定を作る_固定した実行条件で反復調整を測定器へ委ねる()
    {
        // Arrange
        Characteristic[] automaticallyAdjusted =
        [
            RunMode.WarmupCountCharacteristic,
            RunMode.IterationCountCharacteristic,
            RunMode.InvocationCountCharacteristic,
            RunMode.UnrollFactorCharacteristic,
            RunMode.IterationTimeCharacteristic,
            RunMode.MinIterationCountCharacteristic,
            RunMode.MaxIterationCountCharacteristic,
            RunMode.MinWarmupIterationCountCharacteristic,
            RunMode.MaxWarmupIterationCountCharacteristic,
        ];

        // Act
        var job = BenchmarkConfig.Create().GetJobs().Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(job.Environment.Runtime).IsEqualTo(CoreRuntime.Core10_0);
            await Assert.That(job.Environment.Platform).IsEqualTo(Platform.X64);
            await Assert.That(job.Infrastructure.BuildConfiguration).IsEqualTo("Release");
            await Assert.That(job.Run.RunStrategy).IsEqualTo(RunStrategy.Throughput);
            await Assert.That(job.Run.LaunchCount).IsEqualTo(1);
            await Assert.That(job.Run.MemoryRandomization).IsFalse();
            await Assert.That(job.HasValue(RunMode.RunStrategyCharacteristic)).IsTrue();
            await Assert.That(job.HasValue(RunMode.MemoryRandomizationCharacteristic)).IsTrue();
            await Assert.That(job.Environment.PowerPlanMode).IsEqualTo(Guid.Empty);
            await Assert.That(job.Accuracy.OutlierMode).IsEqualTo(OutlierMode.RemoveUpper);
            await Assert.That(job.HasValue(AccuracyMode.OutlierModeCharacteristic)).IsTrue();

            foreach (var characteristic in automaticallyAdjusted)
            {
                await Assert.That(job.HasValue(characteristic)).IsFalse();
            }
        }
    }

    [Test]
    public async Task 本測定設定を作る_反復ごとの時間とmanaged割り当てを再集計できる()
    {
        // Arrange
        var expectedExporters = new[] { JsonExporter.Full, CsvMeasurementsExporter.Default };

        // Act
        var config = BenchmarkConfig.Create();

        // Assert
        using (Assert.Multiple())
        {
            foreach (var exporter in expectedExporters)
            {
                await Assert.That(config.GetExporters()).Contains(exporter);
            }

            await Assert.That(config.GetDiagnosers()).Contains(MemoryDiagnoser.Default);
        }
    }
}
