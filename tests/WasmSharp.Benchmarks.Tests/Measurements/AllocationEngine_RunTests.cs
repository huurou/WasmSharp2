using System.Runtime.CompilerServices;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Mathematics.OutlierDetection;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

[NotInParallel]
internal class AllocationEngine_RunTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 追加反復を終える_成功と例外のどちらでもイベント購読がエンジンを保持しない(bool fails)
    {
        // Arrange
        WeakReference? reference = null;

        if (fails)
        {
            // Act & Assert
            await Assert
                .That(() => RunAndReleaseEngine(x => reference = x, fails))
                .ThrowsExactly<InvalidOperationException>();
        }
        else
        {
            // Act
            RunAndReleaseEngine(x => reference = x, fails);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        await Assert.That(reference!.IsAlive).IsFalse();
    }

    [Test]
    public async Task 追加反復が失敗する_反復cleanupを実行してglobalCleanupはDisposeへ委ねる()
    {
        // Arrange
        var expected = new InvalidOperationException("追加反復失敗");
        List<string> callbacks = [];
        var timing = new AllocationEngineFixture
        {
            GlobalSetupAction = () => callbacks.Add("global setup"),
            GlobalCleanupAction = () => callbacks.Add("global cleanup"),
            WorkloadAction = _ => throw expected,
        };
        var parameters = new EngineParameters
        {
            MeasureExtraStats = true,
            IterationSetupAction = () => callbacks.Add("iteration setup"),
            IterationCleanupAction = () => callbacks.Add("iteration cleanup"),
        };
        using (var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters))
        {
            // Act & Assert
            var actual = await Assert.That(engine.Run).ThrowsExactly<InvalidOperationException>();
            using (Assert.Multiple())
            {
                await Assert.That(actual).IsSameReferenceAs(expected);
                await Assert
                    .That(callbacks.SequenceEqual(["global setup", "iteration setup", "iteration cleanup"]))
                    .IsTrue();
                await Assert.That(timing.Disposed).IsFalse();
            }
        }

        await Assert
            .That(callbacks.SequenceEqual(["global setup", "iteration setup", "iteration cleanup", "global cleanup"]))
            .IsTrue();
    }

    [Test]
    public async Task 時間計測が失敗する_追加反復を開始せず同じ例外を伝播する()
    {
        // Arrange
        var expected = new InvalidOperationException("時間計測失敗");
        var setupCount = 0;
        var timing = new AllocationEngineFixture { RunFailure = expected };
        var parameters = new EngineParameters { MeasureExtraStats = true, IterationSetupAction = () => setupCount++ };
        using var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters);

        // Act & Assert
        var actual = await Assert.That(engine.Run).ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsSameReferenceAs(expected);
            await Assert.That(setupCount).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Workload内で例外を捕捉する_追加反復の操作数で例外頻度を返し次のRunへ持ち越さない()
    {
        // Arrange
        var timing = new AllocationEngineFixture
        {
            WorkloadAction = count =>
            {
                for (long index = 0; index < count; index++)
                {
                    ThrowAndCatch();
                }
            },
            Results = new RunResults(
                [
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 1, 2, 50),
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 2, 4, 100),
                ],
                OutlierMode.RemoveUpper,
                default,
                default,
                0
            ),
        };
        var parameters = new EngineParameters
        {
            MeasureExtraStats = true,
            IterationSetupAction = ThrowAndCatch,
            IterationCleanupAction = ThrowAndCatch,
        };
        using var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters);

        // Act
        var result = engine.Run();
        timing.WorkloadAction = _ => { };
        var next = engine.Run();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExceptionFrequency).IsEqualTo(1d);
            await Assert.That(result.GCStats.TotalOperations).IsEqualTo(4);
            await Assert.That(next.ExceptionFrequency).IsEqualTo(0d);
            await Assert.That(next.GCStats.TotalOperations).IsEqualTo(4);
        }
    }

    [Test]
    public async Task 追加反復で配列を割り当てFullGCを行う_統計はRun全体の増分を超えない()
    {
        // Arrange
        byte[]? allocated = null;
        var timing = new AllocationEngineFixture
        {
            WorkloadAction = _ =>
            {
                allocated = new byte[1024];
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            },
        };
        var parameters = new EngineParameters { MeasureExtraStats = true };
        using var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters);

        // Act
        // 背景のGCや処理も含むため、Run全体の増分を測定区間の上限にする。
        var contentionBefore = Monitor.LockContentionCount;
        var completedBefore = ThreadPool.CompletedWorkItemCount;
        int[] collectionsBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var result = engine.Run();
        int[] collectionsAfter = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var completedAfter = ThreadPool.CompletedWorkItemCount;
        var contentionAfter = Monitor.LockContentionCount;

        // Assert
        await Assert.That(allocated).IsNotNull();
        using (Assert.Multiple())
        {
            for (var generation = 0; generation < collectionsBefore.Length; generation++)
            {
                var measured = result.GCStats.GetCollectionsCount(generation);
                await Assert.That(measured).IsGreaterThanOrEqualTo(1);
                await Assert
                    .That(measured)
                    .IsLessThanOrEqualTo(collectionsAfter[generation] - collectionsBefore[generation]);
            }
            await Assert
                .That(result.GCStats.GetTotalAllocatedBytes(excludeAllocationQuantumSideEffects: false).GetValueOrDefault())
                .IsGreaterThanOrEqualTo(allocated!.LongLength);
            await Assert
                .That(result.ThreadingStats.CompletedWorkItemCount)
                .IsLessThanOrEqualTo(completedAfter - completedBefore);
            await Assert
                .That(result.ThreadingStats.LockContentionCount)
                .IsLessThanOrEqualTo(contentionAfter - contentionBefore);
        }
    }

    [Test]
    [Arguments(1, 15)]
    [Arguments(4, 60)]
    public async Task 時間計測が完了する_選択済みunrollと最後の操作数で追加統計を測り時間結果を保持する(
        int effectiveUnroll,
        long totalOperations
    )
    {
        // Arrange
        long measuredInvocations = 0;
        var timing = new AllocationEngineFixture
        {
            TargetJob = Job.Default.WithUnrollFactor(effectiveUnroll).WithOutlierMode(OutlierMode.DontRemove),
            OperationsPerInvoke = 3,
            WorkloadAction = count => measuredInvocations = count,
            Results = new RunResults(
                [
                    new Measurement(0, IterationMode.Workload, IterationStage.Jitting, 1, 3, 300),
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 1, totalOperations, 100),
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 2, totalOperations, 101),
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 3, totalOperations, 99),
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 4, totalOperations, 100),
                    new Measurement(0, IterationMode.Workload, IterationStage.Actual, 5, totalOperations, 10000),
                    new Measurement(0, IterationMode.Overhead, IterationStage.Actual, 1, 4096, 10),
                ],
                OutlierMode.DontRemove,
                default,
                default,
                0
            ),
        };
        var parameters = new EngineParameters { MeasureExtraStats = true, TargetJob = Job.Default.WithUnrollFactor(16) };
        using var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters);

        // Act
        var result = engine.Run();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(timing.RunCount).IsEqualTo(1);
            await Assert.That(measuredInvocations).IsEqualTo(5);
            await Assert.That(result.GCStats.TotalOperations).IsEqualTo(totalOperations);
            await Assert.That(result.ThreadingStats.TotalOperations).IsEqualTo(totalOperations);
            await Assert.That(result.EngineMeasurements).IsSameReferenceAs(timing.Results.EngineMeasurements);
            await Assert
                .That(
                    result
                        .GetWorkloadResultMeasurements()
                        .Select(x => x.Nanoseconds)
                        .SequenceEqual([90d, 91d, 89d, 90d, 9990d])
                )
                .IsTrue();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunAndReleaseEngine(Action<WeakReference> observeEngine, bool fails)
    {
        var timing = new AllocationEngineFixture
        {
            WorkloadAction = _ =>
            {
                if (fails)
                {
                    throw new InvalidOperationException("購読解除の確認対象");
                }
            },
        };
        using var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(
            new EngineParameters { MeasureExtraStats = true }
        );
        observeEngine(new WeakReference(engine));
        engine.Run();
    }

    private static void ThrowAndCatch()
    {
        try
        {
            throw new InvalidOperationException("例外頻度の測定対象");
        }
        catch (InvalidOperationException) { }
    }
}
