using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Mathematics.OutlierDetection;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class AllocationEngineFixture : IEngineFactory, IEngine
{
    public bool? RequestedExtraStats { get; private set; }
    public bool Disposed { get; private set; }
    public int RunCount { get; private set; }
    public IHost Host => null!;
    public Job TargetJob { get; set; } = Job.Default.WithUnrollFactor(1);
    public long OperationsPerInvoke { get; set; } = 1;
    public Action? GlobalSetupAction { get; set; }
    public Action? GlobalCleanupAction { get; set; }
    public Action<long> WorkloadAction { get; set; } = _ => { };
    public Action<long> OverheadAction => _ => { };
    public IResolver Resolver => EngineParameters.DefaultResolver;
    public RunResults Results { get; set; } =
        new(
            [new Measurement(0, IterationMode.Workload, IterationStage.Actual, 1, 1, 100)],
            OutlierMode.RemoveUpper,
            default,
            default,
            0
        );
    public Exception? CreationFailure { get; set; }
    public Exception? RunFailure { get; set; }

    public IEngine CreateReadyToRun(EngineParameters engineParameters)
    {
        RequestedExtraStats = engineParameters.MeasureExtraStats;
        if (CreationFailure is not null)
        {
            throw CreationFailure;
        }

        GlobalSetupAction?.Invoke();
        return this;
    }

    public RunResults Run()
    {
        RunCount++;
        if (RunFailure is not null)
        {
            throw RunFailure;
        }

        return Results;
    }

    public void Dispose()
    {
        Disposed = true;
        GlobalCleanupAction?.Invoke();
    }

    public Measurement RunIteration(IterationData data) => throw new NotSupportedException();

    public void WriteLine() { }

    public void WriteLine(string line) { }
}
