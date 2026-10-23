using System.Runtime.InteropServices;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_CaptureEnvironmentTests
{
    [Test]
    public async Task 現在の環境と診断構成を採取する_完全版と実効値と実行条件だけをrunへ保存する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        var command = new CommandInvocation(
            Environment.ProcessPath!,
            ["--diagnose", "value with spaces"],
            Environment.CurrentDirectory
        );
        var process = new ProcessConfiguration(
            "Diagnostic",
            "diagnostic-job",
            1,
            [new("Strategy", "Throughput")],
            [new("System.GC.Server", "false")],
            [new("DOTNET_EnableHWIntrinsic", "0"), new("UNRELATED_API_TOKEN", "must not be saved")]
        );

        // Act
        var environment = BenchmarkRun.CaptureEnvironment(store, command, [process]);

        // Assert
        var saved = JsonSerializer.Deserialize<RunEnvironment>(
            File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, "environment.json")),
            CaseJson.Options
        )!;
        using (Assert.Multiple())
        {
            await Assert.That(environment.Cpu.Length > 0).IsTrue();
            await Assert
                .That(environment.LogicalProcessorCount)
                .IsEqualTo(Environment.ProcessorCount);
            await Assert
                .That(environment.OperatingSystem)
                .IsEqualTo(RuntimeInformation.OSDescription);
            await Assert
                .That(environment.OsArchitecture)
                .IsEqualTo(RuntimeInformation.OSArchitecture.ToString());
            await Assert
                .That(environment.ProcessArchitecture)
                .IsEqualTo(RuntimeInformation.ProcessArchitecture.ToString());
            await Assert.That(environment.RuntimeVersion).Contains(Environment.Version.ToString());
            await Assert.That(environment.SdkVersion).IsNotEqualTo("10.0");
            await Assert.That(environment.BenchmarkDotNetVersion).Contains("0.15.8");
            await Assert.That(environment.BuildConfiguration).IsEqualTo("Release");
            await Assert
                .That(environment.GcSettings.Single(x => x.Name == "GCSettings.IsServerGC").Value)
                .IsEqualTo(System.Runtime.GCSettings.IsServerGC.ToString());
            await Assert
                .That(
                    environment
                        .JitSettings.Single(x => x.Name == "Effective.TieredCompilation")
                        .Value
                )
                .Contains("Unavailable:");
            await Assert
                .That(saved.Command.Arguments.SequenceEqual(["--diagnose", "value with spaces"]))
                .IsTrue();
            await Assert
                .That(
                    saved
                        .Processes.Single(x => x.Role == "Diagnostic")
                        .EnvironmentVariables.Select(x => x.Name)
                )
                .IsEquivalentTo(["DOTNET_EnableHWIntrinsic"]);
            await Assert
                .That(File.ReadAllText(Path.Combine(fixture.DirectoryPath, "environment.json")))
                .DoesNotContain("must not be saved");
            await Assert
                .That(
                    saved
                        .Processes.Single(x => x.Role == "Collector")
                        .RuntimeConfiguration.Select(x => x.Name)
                )
                .Contains("runtimeOptions.framework.version");
            await Assert
                .That(
                    File.Exists(
                        Path.Combine(fixture.DirectoryPath, "settings/collector.runtimeconfig.json")
                    )
                )
                .IsTrue();
        }
    }
}
