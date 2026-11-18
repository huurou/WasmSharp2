using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class BenchmarkProcessFixture : IDisposable
{
    private readonly CaseJson json_ = new CaseJson();

    private readonly string? previous_ = Environment.GetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE);
    internal AssetLoadingFixture Assets { get; } = new();
    internal RunStoreFixture Run { get; } = new();

    internal BenchmarkProcessFixture()
    {
        RunStore.Create(Run.DirectoryPath, Run.Manifest, json_);
        var configuration = new RunConfiguration(
            Run.Manifest.RunId,
            Run.DirectoryPath,
            Assets.ManifestPath,
            "SmokeJob",
            ["invoke.single"]
        );
        var path = Path.Combine(Run.DirectoryPath, "config.json");
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(configuration, json_.Options));
        Environment.SetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE, path);
    }

    internal void BlockCheckWrites()
    {
        var checks = Path.Combine(Run.DirectoryPath, "checks");
        Directory.Move(checks, checks + "-saved");
        File.WriteAllText(checks, "保存失敗用のファイル");
    }

    internal void RestoreCheckWrites()
    {
        var checks = Path.Combine(Run.DirectoryPath, "checks");
        File.Delete(checks);
        Directory.Move(checks + "-saved", checks);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE, previous_);
        Assets.Dispose();
        Run.Dispose();
    }
}
