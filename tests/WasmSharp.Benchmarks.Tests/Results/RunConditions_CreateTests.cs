using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunConditions_CreateTests
{
    private readonly CaseJson json_ = new CaseJson();

    [Test]
    public async Task 複数の素材とprocessの列挙順だけが異なる_Jobごとの観測を保って昇順の条件を作る()
    {
        // Arrange
        using var fixture = new RunComparisonFixture("run", "repository");
        using var template = new RunStoreFixture();
        var definition = fixture.Cases.Single() with
        {
            Workload =
            [
                new WorkloadQuantity("z", 2, WorkloadUnit.Iterations),
                new WorkloadQuantity("a", 1, WorkloadUnit.Iterations),
            ],
        };
        var asset = fixture.Assets.Assets.Single() with
        {
            Sources =
            [
                new AssetSource("sources/z.wat", "z-revision", "z-hash"),
                new AssetSource("sources/a.wat", "a-revision", "a-hash"),
            ],
        };
        var assets = fixture.Assets with { Assets = [asset] };
        var source = fixture.Source with
        {
            Submodules =
            [
                new SubmoduleRevision("tools/z", "z-revision", false),
                new SubmoduleRevision("tools/a", "a-revision", true),
            ],
        };
        List<ProcessConfiguration> jobs =
        [
            fixture.Jobs.Single() with { JobId = "z-job", Role = "z-role", LaunchCount = 2 },
            fixture.Jobs.Single() with { JobId = "a-job", Role = "a-role", LaunchCount = 1 },
        ];
        List<ProcessCheck> checks =
        [
            template.Check with { JobId = "z-job", AffinityBefore = 8, RuntimeVersion = "z-runtime" },
            template.Check with { JobId = "a-job", AffinityBefore = 4, RuntimeVersion = "a-runtime" },
        ];
        var environment = fixture.Environment with { Processes = jobs };

        // Act
        var original = RunConditions.Create(
            fixture.Manifest, definition, assets, environment, source, jobs, checks,
            json_
        );
        var reordered = RunConditions.Create(
            fixture.Manifest,
            definition with { Workload = [.. definition.Workload.AsEnumerable().Reverse()] },
            assets with { Assets = [asset with { Sources = [.. asset.Sources.AsEnumerable().Reverse()] }] },
            environment with { Processes = [.. jobs.AsEnumerable().Reverse()] },
            source with { Submodules = [.. source.Submodules.AsEnumerable().Reverse()] },
            [.. jobs.AsEnumerable().Reverse()],
            [.. checks.AsEnumerable().Reverse()],
            json_
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reordered.Values).IsEquivalentTo(original.Values);
            await Assert.That(reordered.Values["case.workload[0].name"]).IsEqualTo("a");
            await Assert.That(reordered.Values["case.workload[1].name"]).IsEqualTo("z");
            await Assert.That(reordered.Values["asset.sources[0].relative_path"]).IsEqualTo("sources/a.wat");
            await Assert.That(reordered.Values["asset.sources[1].relative_path"]).IsEqualTo("sources/z.wat");
            await Assert.That(reordered.Values["source.submodules[0].relative_path"]).IsEqualTo("tools/a");
            await Assert.That(reordered.Values["source.submodules[1].relative_path"]).IsEqualTo("tools/z");
            await Assert.That(reordered.Values["environment.processes[0].role"]).IsEqualTo("a-role");
            await Assert.That(reordered.Values["environment.processes[1].role"]).IsEqualTo("z-role");
            await Assert.That(reordered.Values["job.processes[0].role"]).IsEqualTo("a-role");
            await Assert.That(reordered.Values["job.processes[1].role"]).IsEqualTo("z-role");
            await Assert.That(reordered.Values["process.affinity[0].mask"]).IsEqualTo("4");
            await Assert.That(reordered.Values["process.runtime[0].version"]).IsEqualTo("a-runtime");
            await Assert.That(reordered.Values["process.affinity[1].mask"]).IsEqualTo("8");
            await Assert.That(reordered.Values["process.runtime[1].version"]).IsEqualTo("z-runtime");
        }
    }

    [Test]
    public async Task ツールと素材の配置を渡す_比較条件にはpathの役割を保持する()
    {
        // Arrange
        using var fixture = new RunComparisonFixture("run", "repository");
        using var template = new RunStoreFixture();
        var environment = fixture.Environment with
        {
            EnvironmentVariables =
            [
                new NamedSetting("DOTNET_ROOT", Path.Combine(fixture.RepositoryDirectory, "dotnet")),
            ],
        };
        var check = template.Check with { JobId = fixture.Jobs.Single().JobId };

        // Act
        var actual = RunConditions.Create(
            fixture.Manifest, fixture.Cases.Single(), fixture.Assets, environment,
            fixture.Source, fixture.Jobs, [check], json_
        );

        // Assert
        var arguments = actual.Values
            .Where(x => x.Key.StartsWith("command.arguments[", StringComparison.Ordinal))
            .Select(x => x.Value);
        using (Assert.Multiple())
        {
            await Assert.That(actual.Values["command.executable"]).IsEqualTo("tool:dotnet");
            await Assert.That(actual.Values["environment.variables.DOTNET_ROOT"]).IsEqualTo("tool-root:dotnet");
            await Assert.That(arguments).Contains("--asset-manifest=asset-manifest");
        }
    }
}
