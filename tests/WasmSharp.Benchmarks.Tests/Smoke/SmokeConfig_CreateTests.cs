using System.Text.Json;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Smoke;

namespace WasmSharp.Benchmarks.Tests.Smoke;

internal class SmokeConfig_CreateTests
{
    [Test]
    public async Task 試走設定を作る_本測定と同じ割り当て測定を選ぶ()
    {
        // Arrange
        // Act
        var job = SmokeConfig.Create().GetJobs().Single();
        var factory = job.ResolveValue(
            InfrastructureMode.EngineFactoryCharacteristic,
            EngineParameters.DefaultResolver
        );

        // Assert
        await Assert.That(factory.GetType().Name).IsEqualTo("WasmAllocationEngineFactory");
    }

    [Test]
    public async Task 保存した設定fixtureを使う_別process用の最小試走条件を復元する()
    {
        // Arrange
        using var fixture = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "smoke-config.json")
            )
        );
        var expected = fixture.RootElement;

        // Act
        var job = SmokeConfig.Create().GetJobs().Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(job.Id).IsEqualTo(expected.GetProperty("jobId").GetString());
            await Assert
                .That(job.Environment.Runtime?.Name)
                .IsEqualTo(expected.GetProperty("runtime").GetString());
            await Assert
                .That(job.Environment.Platform.ToString())
                .IsEqualTo(expected.GetProperty("platform").GetString());
            await Assert
                .That(job.Infrastructure.BuildConfiguration)
                .IsEqualTo(expected.GetProperty("buildConfiguration").GetString());
            await Assert
                .That(job.Run.RunStrategy.ToString())
                .IsEqualTo(expected.GetProperty("runStrategy").GetString());
            await Assert
                .That(job.Run.LaunchCount)
                .IsEqualTo(expected.GetProperty("launchCount").GetInt32());
            await Assert
                .That(job.Run.WarmupCount)
                .IsEqualTo(expected.GetProperty("warmupCount").GetInt32());
            await Assert
                .That(job.Run.IterationCount)
                .IsEqualTo(expected.GetProperty("iterationCount").GetInt32());
            await Assert
                .That(job.Run.InvocationCount)
                .IsEqualTo(expected.GetProperty("invocationCount").GetInt64());
            await Assert
                .That(job.Run.UnrollFactor)
                .IsEqualTo(expected.GetProperty("unrollFactor").GetInt32());
            await Assert
                .That(job.Run.MemoryRandomization)
                .IsEqualTo(expected.GetProperty("memoryRandomization").GetBoolean());
            await Assert.That(job.Environment.PowerPlanMode).IsEqualTo(Guid.Empty);
        }
    }

    [Test]
    public async Task 試走設定を作る_個々の測定値とmanaged割り当てを保存できる()
    {
        // Arrange
        var expectedExporters = new[] { JsonExporter.Full, CsvMeasurementsExporter.Default };

        // Act
        var config = SmokeConfig.Create();

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
