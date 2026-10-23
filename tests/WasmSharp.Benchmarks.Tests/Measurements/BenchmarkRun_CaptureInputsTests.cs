using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_CaptureInputsTests
{
    [Test]
    public async Task 採用素材と構成を採取する_生成済み素材とケースとJob設定を同じrunへ対応付ける()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        using var run = new RunStoreFixture();
        var store = RunStore.Create(run.DirectoryPath, run.Manifest);
        var processes = new List<ProcessConfiguration>
        {
            new("Measurement", "job-1", 1, [new("Strategy", "Throughput")], [], []),
        };

        // Act
        BenchmarkRun.CaptureInputs(store, fixture.ManifestPath, processes);

        // Assert
        var manifest = JsonSerializer.Deserialize<RunManifest>(
            File.ReadAllBytes(Path.Combine(run.DirectoryPath, "run.json")),
            CaseJson.Options
        )!;
        var copied = AssetManifest.Load(
            Path.Combine(run.DirectoryPath, manifest.AssetManifestFile!.RelativePath)
        );
        using (Assert.Multiple())
        {
            await Assert
                .That(copied.Manifest.Cases.Sha256)
                .IsEqualTo(fixture.Manifest.Cases.Sha256);
            await Assert.That(copied.GetBinary("invoke").Length > 0).IsTrue();
            await Assert.That(manifest.CasesFile!.Sha256).IsEqualTo(fixture.Manifest.Cases.Sha256);
            await Assert
                .That(
                    JsonSerializer
                        .Deserialize<List<ProcessConfiguration>>(
                            File.ReadAllBytes(
                                Path.Combine(
                                    run.DirectoryPath,
                                    manifest.JobSettingsFile!.RelativePath
                                )
                            ),
                            CaseJson.Options
                        )!
                        .Single()
                        .Role
                )
                .IsEqualTo("Measurement");
        }
    }
}
