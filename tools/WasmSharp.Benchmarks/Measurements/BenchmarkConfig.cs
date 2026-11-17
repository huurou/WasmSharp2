using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Perfolizer.Mathematics.OutlierDetection;

namespace WasmSharp.Benchmarks.Measurements;

internal static class BenchmarkConfig
{
    internal static IConfig Create()
    {
        var job = Job
            .Default.WithId("Measurement")
            .WithPlatform(Platform.X64)
            .WithCustomBuildConfiguration("Release")
            .WithStrategy(RunStrategy.Throughput)
            .WithEngineFactory(new WasmAllocationEngineFactory())
            .WithLaunchCount(1)
            .WithMemoryRandomization(false)
            .WithOutlierMode(OutlierMode.RemoveUpper)
            .DontEnforcePowerPlan();

        return CreateConfig(job);
    }

    internal static IConfig CreateConfig(Job job)
    {
        var runtime = CoreRuntime.CreateForNewVersion("net11.0", ".NET 11.0");
        var csProj = CsProjCoreToolchain.From(
            new NetCoreAppSettings(runtime.MsBuildMoniker, runtimeFrameworkVersion: null, name: runtime.Name)
        );
        // BDN 0.15.8のSDK事前検証と並列ビルド判定は.NET 11を扱えないため、SDKの検証は実際のビルドに任せる。
        var toolchain = new Toolchain(csProj.Name, csProj.Generator, csProj.Builder, csProj.Executor);
        return ManualConfig
            .Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableParallelBuild)
            .AddJob(job.WithRuntime(runtime).WithToolchain(toolchain))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default);
    }
}
