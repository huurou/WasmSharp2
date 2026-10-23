using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Measurements;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_DescribeProcessTests
{
    [Test]
    public async Task 本測定Jobを記録する_明示設定と既定の反復調整と関連環境変数を分ける()
    {
        // Arrange
        var job = BenchmarkConfig
            .Create()
            .GetJobs()
            .Single()
            .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0")
            .WithEnvironmentVariable("WASMSHARP_BENCHMARK_RUN_CONFIG", "/runs/config.json")
            .WithEnvironmentVariable("UNRELATED_API_TOKEN", "must not be saved");

        // Act
        var process = BenchmarkRun.DescribeProcess("Measurement", job);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(process.JobId).IsEqualTo("Measurement");
            await Assert.That(process.LaunchCount).IsEqualTo(1);
            await Assert
                .That(process.JobSettings.Single(x => x.Name == "RunMode.RunStrategy").Value)
                .IsEqualTo("Throughput");
            await Assert
                .That(
                    process.JobSettings.Single(x => x.Name == "EnvironmentMode.PowerPlanMode").Value
                )
                .IsEqualTo("00000000-0000-0000-0000-000000000000");
            await Assert
                .That(process.JobSettings.Single(x => x.Name == "RunMode.IterationCount").Value)
                .IsEqualTo("Default");
            await Assert
                .That(process.EnvironmentVariables.Select(x => x.Name))
                .IsEquivalentTo(["DOTNET_EnableHWIntrinsic", "WASMSHARP_BENCHMARK_RUN_CONFIG"]);
            await Assert
                .That(process.JobSettings.Any(x => x.Value.Contains("must not be saved")))
                .IsFalse();
        }
    }
}
