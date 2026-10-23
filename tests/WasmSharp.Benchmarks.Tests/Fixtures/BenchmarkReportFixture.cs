using System.Globalization;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.Results;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class BenchmarkReportFixture : IDisposable
{
    private readonly RunStoreFixture run_ = new();

    internal string DirectoryPath => run_.DirectoryPath;
    internal RunRequest Request { get; }
    internal PreparedRun Prepared { get; }
    internal BenchmarkReport Report { get; }
    internal Summary Summary { get; }

    internal BenchmarkReportFixture(IEnumerable<string>? lines = null)
    {
        var config = BenchmarkConfig.Create();
        var benchmark = BenchmarkConverter
            .MethodsToBenchmarks(
                typeof(InvokeBenchmarks),
                [typeof(InvokeBenchmarks).GetMethod(nameof(InvokeBenchmarks.Single))!],
                config
            )
            .BenchmarksCases.Single();
        var output = (lines ?? ReadLines()).ToArray();
        var execute = new ExecuteResult(
            true,
            0,
            123,
            output.Where(x => !x.StartsWith("//")).ToArray(),
            output.Where(x => x.StartsWith("//")).ToArray(),
            output,
            1
        );
        var generate = GenerateResult.Success(null!, []);
        Report = new(true, benchmark, generate, BuildResult.Success(generate), [execute], []);
        Summary = new(
            "固定report fixture",
            [Report],
            HostEnvironmentInfo.GetCurrent(),
            DirectoryPath,
            Path.Combine(DirectoryPath, "fixture.log"),
            TimeSpan.Zero,
            CultureInfo.InvariantCulture,
            [],
            []
        );
        Prepared = new(
            RunStore.Create(DirectoryPath, run_.Manifest),
            run_.Manifest.RunId,
            [benchmark.Job]
        );
        Request = new(
            DirectoryPath,
            "fixtureで素材を読み込まない",
            ["invoke.single"],
            RunKind.Smoke,
            config,
            "",
            new("fixture", [], "")
        );
        Prepared.Store.SaveFile(
            "raw/bdn-output.txt",
            System.Text.Encoding.UTF8.GetBytes(string.Join('\n', output))
        );
    }

    internal static string[] ReadLines() =>
        File.ReadAllLines(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "bdn-0.15.8-metrics.txt")
        );

    public void Dispose() => run_.Dispose();
}
