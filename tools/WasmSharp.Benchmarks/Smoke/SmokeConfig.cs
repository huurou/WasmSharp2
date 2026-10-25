using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Measurements;

namespace WasmSharp.Benchmarks.Smoke;

internal static class SmokeConfig
{
    internal static IConfig Create()
    {
        // 試走に必要な最小反復数 本測定の統計には使用しない。
        var job = Job
            .Default.WithId("Smoke")
            .WithRuntime(CoreRuntime.Core10_0)
            .WithPlatform(Platform.X64)
            .WithCustomBuildConfiguration("Release")
            .WithStrategy(RunStrategy.Throughput)
            .WithEngineFactory(new WasmAllocationEngineFactory())
            .WithLaunchCount(1)
            .WithWarmupCount(1)
            .WithIterationCount(3)
            .WithInvocationCount(64)
            .WithUnrollFactor(1)
            .WithMemoryRandomization(false)
            .DontEnforcePowerPlan();

        return ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default);
    }
}
