using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_PrepareTests
{
    private readonly CaseJson _json = new CaseJson();
    private readonly MeasurementMethods _methods = new MeasurementMethods();
    private readonly BenchmarkRun _benchmarkRun;

    public BenchmarkRun_PrepareTests()
    {
        _benchmarkRun = new BenchmarkRun(_json, _methods);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 予定ケースが空または重複する_準備失敗を保存して測定を開始しない(bool duplicated)
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var run = new RunStoreFixture();
        var request = new RunRequest(
            run.DirectoryPath,
            assets.ManifestPath,
            duplicated ? ["invoke.single", "invoke.single"] : [],
            RunKind.Smoke,
            BenchmarkConfig.Create(),
            "",
            new("dotnet", [], "")
        );

        // Act & Assert
        await Assert.That(() => _benchmarkRun.Prepare(request)).ThrowsExactly<ArgumentException>();
        var manifest = RunStore.Load(run.DirectoryPath, _json);
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.CompletedAtUtc).IsNull();
            await Assert
                .That(manifest.Failures.Single(x => x.Stage == RunStage.Preparation).Message)
                .Contains("重複のない1件以上");
            await Assert
                .That(manifest.Failures.Single(x => x.Stage == RunStage.Preparation).Reason)
                .Contains("runの準備に失敗");
            await Assert.That(Directory.Exists(Path.Combine(run.DirectoryPath, "raw"))).IsFalse();
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
        var prepared = _benchmarkRun.Prepare(request);
        var manifest = RunStore.Load(run.DirectoryPath, _json);
        var variable = prepared
            .Jobs.Single()
            .Environment.EnvironmentVariables.SingleOrDefault(x => x.Key == RunConfiguration.ENVIRONMENT_VARIABLE);

        // Assert
        await Assert.That(variable).IsNotNull();
        var configuration = JsonSerializer.Deserialize<RunConfiguration>(File.ReadAllBytes(variable!.Value), _json.Options)!;
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.PlannedCaseIds).IsEquivalentTo(["invoke.single"]);
            await Assert.That(configuration.RunId).IsEqualTo(manifest.RunId);
            await Assert.That(configuration.JobId).IsEqualTo("Measurement");
            await Assert.That(Path.IsPathFullyQualified(variable.Value)).IsTrue();
            await Assert.That(Path.GetDirectoryName(variable.Value)).IsEqualTo(Path.Combine(run.DirectoryPath, "inputs"));
            await Assert.That(Path.GetExtension(variable.Value)).IsEqualTo(".json");
            await Assert.That(configuration.OutputDirectory).IsEqualTo(run.DirectoryPath);
            await Assert
                .That(configuration.AssetManifestPath)
                .IsEqualTo(Path.Combine(run.DirectoryPath, "inputs", "assets", "asset-manifest.json"));
            await Assert
                .That(
                    AssetManifest.Load(configuration.AssetManifestPath, _json, _methods).Catalog.Get("invoke.single").CaseId
                )
                .IsEqualTo("invoke.single");
        }
    }
}
