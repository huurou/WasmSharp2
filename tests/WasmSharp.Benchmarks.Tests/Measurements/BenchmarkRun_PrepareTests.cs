using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_PrepareTests
{
    [Test]
    public async Task 予定ケースの準備が失敗_予定を保存したrunへ理由を残して未完了にする()
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var run = new RunStoreFixture();
        var request = new RunRequest(
            run.DirectoryPath,
            assets.ManifestPath,
            ["unknown"],
            RunKind.Smoke,
            BenchmarkConfig.Create(),
            "",
            new("dotnet", [], "")
        );

        // Act & Assert
        await Assert.That(() => BenchmarkRun.Prepare(request)).Throws<InvalidDataException>();
        var manifest = RunStore.Load(run.DirectoryPath);
        using (Assert.Multiple())
        {
            await Assert.That(manifest.PlannedCaseIds).IsEquivalentTo(["unknown"]);
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert
                .That(
                    manifest.Failures.Any(x =>
                        x.Stage == RunStage.Preparation && x.Message!.Contains("unknown")
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 新規runを準備する_予定とコピー済み素材の絶対pathをJob別設定で子へ渡す()
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var run = new RunStoreFixture();
        var request = new RunRequest(
            run.DirectoryPath,
            assets.ManifestPath,
            ["invoke.single"],
            RunKind.Smoke,
            BenchmarkConfig.Create(),
            "",
            new("dotnet", [], "")
        );

        // Act
        var prepared = BenchmarkRun.Prepare(request);
        var manifest = RunStore.Load(run.DirectoryPath);
        var variable = prepared
            .Jobs.Single()
            .Environment.EnvironmentVariables.SingleOrDefault(x =>
                x.Key == RunConfiguration.ENVIRONMENT_VARIABLE
            );

        // Assert
        await Assert.That(variable).IsNotNull();
        var configuration = JsonSerializer.Deserialize<RunConfiguration>(
            File.ReadAllBytes(variable!.Value),
            CaseJson.Options
        )!;
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.PlannedCaseIds).IsEquivalentTo(["invoke.single"]);
            await Assert.That(configuration.RunId).IsEqualTo(manifest.RunId);
            await Assert.That(configuration.JobId).IsEqualTo("Measurement");
            await Assert.That(Path.IsPathFullyQualified(variable.Value)).IsTrue();
            await Assert.That(configuration.OutputDirectory).IsEqualTo(run.DirectoryPath);
            await Assert
                .That(configuration.AssetManifestPath)
                .IsEqualTo(
                    Path.Combine(run.DirectoryPath, "inputs", "assets", "asset-manifest.json")
                );
            await Assert
                .That(
                    AssetManifest
                        .Load(configuration.AssetManifestPath)
                        .Catalog.Get("invoke.single")
                        .CaseId
                )
                .IsEqualTo("invoke.single");
        }
    }
}
