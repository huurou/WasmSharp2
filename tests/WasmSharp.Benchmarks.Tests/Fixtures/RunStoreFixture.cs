using System.Text.Json;
using System.Text.Json.Nodes;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class RunStoreFixture : IDisposable
{
    internal string DirectoryPath { get; } =
        Path.Combine(Path.GetTempPath(), "wasm-benchmark-run-" + Guid.NewGuid().ToString("N"));

    internal RunManifest Manifest { get; }
    internal RunEnvironment Environment { get; }
    internal RunSource Source { get; private set; }
    internal ProcessCheck Check { get; }

    internal RunStoreFixture()
    {
        var fixture = JsonNode.Parse(
            File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "run-succeeded.json")
            )
        )!;
        Manifest = fixture["run"]!.Deserialize<RunManifest>(CaseJson.Options)!;
        Environment = fixture["environment"]!.Deserialize<RunEnvironment>(CaseJson.Options)!;
        Source = fixture["source"]!.Deserialize<RunSource>(CaseJson.Options)!;
        Check = fixture["checks"]![0]!.Deserialize<ProcessCheck>(CaseJson.Options)!;
    }

    internal void SaveConditions(RunStore store)
    {
        var assets = store.SaveFile("assets/asset-manifest.json", "{}"u8);
        var cases = store.SaveFile("cases.json", "[]"u8);
        var job = store.SaveFile("job.json", "{}"u8);
        store.SaveInputs(assets, cases, job);
        store.SaveEnvironment(Environment);
        Source = Source with
        {
            IndexDiff = store.SaveFile(Source.IndexDiff.RelativePath, "fixture index diff\n"u8),
            WorktreeDiff = store.SaveFile(
                Source.WorktreeDiff.RelativePath,
                "fixture worktree diff\n"u8
            ),
            UntrackedFiles = Source
                .UntrackedFiles.Select(x =>
                    store.SaveFile(
                        x.RelativePath,
                        "namespace Fixture;\ninternal sealed class NewType;\n"u8
                    )
                )
                .ToList(),
        };
        store.SaveSource(Source);
    }

    internal CaseRunResult SaveSucceededCase(RunStore store)
    {
        var raw = store.SaveFile("raw/full.json", "{\"case\":\"invoke.single\"}"u8);
        var measurements = store.SaveFile("raw/measurements.csv", "operation,ns\n1,125.25"u8);
        var check = store.SaveCheck(Check);
        var result = Manifest.Cases.Single();
        result = result with
        {
            RawFiles = [raw, measurements],
            Metrics = result.Metrics with
            {
                Time = result.Metrics.Time! with { RawMeasurements = measurements },
            },
            BenchmarkReport = result.BenchmarkReport! with
            {
                Processes = [result.BenchmarkReport.Processes.Single() with { CheckFile = check }],
            },
        };
        store.SaveCase(result);
        return result;
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
