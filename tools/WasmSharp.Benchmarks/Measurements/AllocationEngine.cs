using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;

namespace WasmSharp.Benchmarks.Measurements;

internal sealed class AllocationEngine(
    IEngine timingEngine,
    Action? iterationSetup,
    Action? iterationCleanup
) : IEngine
{
    private long exceptionCount_;

    public IHost Host => timingEngine.Host;
    public Job TargetJob => timingEngine.TargetJob;
    public long OperationsPerInvoke => timingEngine.OperationsPerInvoke;
    public Action? GlobalSetupAction => timingEngine.GlobalSetupAction;
    public Action? GlobalCleanupAction => timingEngine.GlobalCleanupAction;
    public Action<long> WorkloadAction => timingEngine.WorkloadAction;
    public Action<long> OverheadAction => timingEngine.OverheadAction;
    public IResolver Resolver => timingEngine.Resolver;

    public RunResults Run()
    {
        var timed = timingEngine.Run();
        var totalOperations = timed
            .EngineMeasurements.Last(x =>
                x.IterationMode == IterationMode.Workload
                && x.IterationStage == IterationStage.Actual
            )
            .Operations;
        // 標準factoryが長時間の処理に選んだunrollを使う。
        var unroll = TargetJob.ResolveValue(RunMode.UnrollFactorCharacteristic, Resolver);
        var invokeCount = totalOperations / OperationsPerInvoke / unroll;

        _ = GcStats.ReadInitial();
        _ = GcStats.ReadFinal();
        iterationSetup?.Invoke();
        try
        {
            var initialThreading = ThreadingStats.ReadInitial();
            Interlocked.Exchange(ref exceptionCount_, 0);
            EventHandler<FirstChanceExceptionEventArgs> listener = OnFirstChanceException;
            AppDomain.CurrentDomain.FirstChanceException += listener;
            GcStats gcStats;
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                // 最後のGCで再登録されたArrayPool清掃も、初期カウンターの読取り前に完了させる。
                GC.WaitForPendingFinalizers();
                gcStats = MeasureAllocations(invokeCount);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= listener;
            }

            var threadingStats = ThreadingStats.ReadFinal() - initialThreading;
            return new RunResults(
                timed.EngineMeasurements,
                TargetJob.ResolveValue(AccuracyMode.OutlierModeCharacteristic, Resolver),
                gcStats.WithTotalOperations(totalOperations),
                threadingStats.WithTotalOperations(totalOperations),
                Interlocked.Read(ref exceptionCount_) / (double)totalOperations
            );
        }
        finally
        {
            iterationCleanup?.Invoke();
        }
    }

    private void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs arguments) =>
        Interlocked.Increment(ref exceptionCount_);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private GcStats MeasureAllocations(long invokeCount)
    {
        var initial = GcStats.ReadInitial();
        WorkloadAction(invokeCount);
        var final = GcStats.ReadFinal();
        return final - initial;
    }

    public Measurement RunIteration(IterationData data) => timingEngine.RunIteration(data);

    public void WriteLine() => timingEngine.WriteLine();

    public void WriteLine(string line) => timingEngine.WriteLine(line);

    public void Dispose() => timingEngine.Dispose();
}
