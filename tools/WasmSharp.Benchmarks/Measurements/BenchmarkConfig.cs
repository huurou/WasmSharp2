using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using Perfolizer.Mathematics.OutlierDetection;

namespace WasmSharp.Benchmarks.Measurements;

internal static class BenchmarkConfig
{
    internal static IConfig Create()
    {
        var job = Job
            .Default.WithId("Measurement")
            .WithRuntime(CoreRuntime.Core10_0)
            .WithPlatform(Platform.X64)
            .WithCustomBuildConfiguration("Release")
            .WithStrategy(RunStrategy.Throughput)
            .WithLaunchCount(1)
            .WithMemoryRandomization(false)
            .WithOutlierMode(OutlierMode.RemoveUpper)
            .DontEnforcePowerPlan();

        return ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default);
    }
}
