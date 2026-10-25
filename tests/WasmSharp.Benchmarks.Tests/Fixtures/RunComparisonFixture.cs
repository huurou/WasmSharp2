using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class RunComparisonFixture : IDisposable
{
    private readonly RunStoreFixture template_ = new();

    internal string DirectoryPath => template_.DirectoryPath;
    internal RunManifest Manifest { get; set; }
    internal RunEnvironment Environment { get; set; }
    internal RunSource Source { get; set; }
    internal List<BenchmarkCase> Cases { get; set; }
    internal AssetManifest Assets { get; set; }
    internal List<ProcessConfiguration> Jobs { get; set; }
    internal long? AffinityBefore { get; set; } = 16384;
    internal long? AffinityAfter { get; set; } = 16384;
    internal string? AffinityUnavailableReason { get; set; }

    internal RunComparisonFixture(string runId, string root)
    {
        Manifest = template_.Manifest with { RunId = runId, Kind = RunKind.Measurement };
        Environment = template_.Environment with
        {
            Command = new(
                root + "/dotnet/dotnet.exe",
                [
                    "run",
                    "--project",
                    root + "/tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj",
                    "-c",
                    "Release",
                    "--",
                    "--asset-manifest",
                    root + "/assets/asset-manifest.json",
                    "--output",
                    root + "/runs/" + runId,
                ],
                root
            ),
            Processes =
            [
                template_.Environment.Processes.Single() with
                {
                    JobId = "auto-" + runId,
                    RuntimeConfiguration =
                    [
                        .. template_.Environment.Processes.Single().RuntimeConfiguration,
                        new("RuntimeConfigFile.RelativePath", root + "/config-" + runId + ".json"),
                        new("RuntimeConfigFile.Sha256", "runtimeconfig-hash"),
                    ],
                    EnvironmentVariables =
                    [
                        new(
                            "WASMSHARP_BENCHMARK_RUN_CONFIG",
                            root + "/runs/" + runId + "/run-config.json"
                        ),
                    ],
                },
            ],
        };
        Source = template_.Source with
        {
            AssembliesBefore =
            [
                new(AssemblyRole.Runtime, root + "/bin/WasmSharp.dll", runId + "-runtime"),
                new(
                    AssemblyRole.Harness,
                    root + "/bin/WasmSharp.Benchmarks.dll",
                    runId + "-harness"
                ),
            ],
            AssembliesAfter =
            [
                new(AssemblyRole.Runtime, root + "/bin/WasmSharp.dll", runId + "-runtime"),
                new(
                    AssemblyRole.Harness,
                    root + "/bin/WasmSharp.Benchmarks.dll",
                    runId + "-harness"
                ),
            ],
            Runtime = template_.Source.Runtime with
            {
                Files =
                [
                    .. template_.Source.Runtime.Files,
                    new("src/WasmSharp/WasmSharp.csproj", "runtime-project"),
                    new("Directory.Build.props", "build-props"),
                ],
            },
            Harness = template_.Source.Harness with
            {
                Files =
                [
                    .. template_.Source.Harness.Files,
                    new(
                        "tools/WasmSharp.Benchmarks/Measurements/InvokeBenchmarks.cs",
                        "harness-code"
                    ),
                ],
            },
        };
        Cases = JsonSerializer
            .Deserialize<List<BenchmarkCase>>(
                File.ReadAllBytes(
                    Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")
                ),
                CaseJson.Options
            )!
            .Where(x => x.CaseId == "invoke.single")
            .Select(x => x with { Workload = [new("calls", 1, WorkloadUnit.Iterations)] })
            .ToList();
        Assets = new(
            1,
            new("core2-profile.json", "profile-hash"),
            new("cases.json", "cases-hash"),
            [
                new(
                    "invoke",
                    new("wasm/invoke.wasm", Cases.Single().AssetSha256),
                    [new("sources/invoke.wat", "source-version", "wat-hash")],
                    [
                        new(
                            new(
                                "wat2wasm",
                                "1.0.42+full",
                                "tool-revision",
                                new("../../tools/wat2wasm.exe", "tool-hash"),
                                "Release"
                            ),
                            ["sources/invoke.wat", "-o", "wasm/invoke.wasm"],
                            "arguments-hash"
                        ),
                    ]
                ),
            ]
        );
        Jobs = Environment.Processes.ToList();
    }

    internal void SetCases(List<BenchmarkCase> cases)
    {
        Cases = cases;
        Manifest = Manifest with
        {
            PlannedCaseIds = cases.Select(x => x.CaseId).ToList(),
            Cases = cases
                .Select(x => template_.Manifest.Cases.Single() with { CaseId = x.CaseId })
                .ToList(),
        };
        Assets = Assets with
        {
            Assets = cases
                .DistinctBy(x => x.AssetId)
                .Select(x =>
                    Assets.Assets.Single() with
                    {
                        AssetId = x.AssetId,
                        Binary = new("wasm/" + x.AssetId + ".wasm", x.AssetSha256),
                    }
                )
                .ToList(),
        };
    }

    internal void Save()
    {
        var store = RunStore.Create(DirectoryPath, Manifest);
        store.SaveInputs(
            SaveJson(store, "assets/asset-manifest.json", Assets),
            SaveJson(store, "cases.json", Cases),
            SaveJson(store, "job.json", Jobs)
        );
        store.SaveEnvironment(Environment);
        store.SaveSource(
            Source with
            {
                IndexDiff = store.SaveFile("source/index.patch", "runtime diff"u8),
                WorktreeDiff = store.SaveFile("source/worktree.patch", "runtime diff"u8),
                UntrackedFiles = [],
            }
        );
        var raw = store.SaveFile("raw/full.json", "{}"u8);
        var measurements = store.SaveFile("raw/measurements.csv", "operations,ns\n1,125.25"u8);
        var processId = Manifest.RunId == "before" ? 4321 : 8765;
        foreach (var result in Manifest.Cases)
        {
            var check = store.SaveCheck(
                template_.Check with
                {
                    RunId = Manifest.RunId,
                    CaseId = result.CaseId,
                    JobId = Jobs.Single().JobId,
                    ProcessId = processId,
                    AffinityBefore = AffinityBefore,
                    AffinityAfter = AffinityAfter,
                    AffinityUnavailableReason = AffinityUnavailableReason,
                }
            );
            store.SaveCase(
                result with
                {
                    RawFiles = [raw, measurements],
                    Metrics = result.Metrics with
                    {
                        Time = result.Metrics.Time is { } time
                            ? time with
                            {
                                RawMeasurements = measurements,
                            }
                            : null,
                    },
                    BenchmarkReport = result.BenchmarkReport! with
                    {
                        Processes =
                        [
                            result.BenchmarkReport.Processes.Single() with
                            {
                                JobId = Jobs.Single().JobId,
                                ProcessId = processId,
                                CheckFile = check,
                            },
                        ],
                    },
                }
            );
        }
        if (Manifest.Status != ResultStatus.Incomplete)
        {
            store.Complete(Manifest.Status, Manifest.Failures);
        }
    }

    private static ManifestFile SaveJson<T>(RunStore store, string path, T value) =>
        store.SaveFile(path, JsonSerializer.SerializeToUtf8Bytes(value, CaseJson.Options));

    public void Dispose() => template_.Dispose();
}
