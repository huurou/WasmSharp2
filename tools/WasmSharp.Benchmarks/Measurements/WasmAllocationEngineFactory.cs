using BenchmarkDotNet.Engines;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 割り当て測定前にファイナライザーの完了を待つBDNエンジンの生成器
/// </summary>
public sealed class WasmAllocationEngineFactory : IEngineFactory
{
    private readonly IEngineFactory timingFactory_;

    public WasmAllocationEngineFactory()
        : this(new EngineFactory()) { }

    internal WasmAllocationEngineFactory(IEngineFactory timingFactory)
    {
        timingFactory_ = timingFactory;
    }

    public IEngine CreateReadyToRun(EngineParameters engineParameters)
    {
        var measureExtraStats = engineParameters.MeasureExtraStats;
        engineParameters.MeasureExtraStats = false;
        try
        {
            var timingEngine = timingFactory_.CreateReadyToRun(engineParameters);
            return measureExtraStats
                ? new AllocationEngine(
                    timingEngine,
                    engineParameters.IterationSetupAction,
                    engineParameters.IterationCleanupAction
                )
                : timingEngine;
        }
        finally
        {
            engineParameters.MeasureExtraStats = measureExtraStats;
        }
    }
}
