using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class AllocationEngine_ConstructorTests
{
    [Test]
    public async Task 時間計測engineを包む_ホストと構成と操作を同じ実体で公開する()
    {
        // Arrange
        var timing = new AllocationEngineFixture
        {
            TargetJob = Job.Default.WithUnrollFactor(4),
            OperationsPerInvoke = 3,
            GlobalSetupAction = () => { },
            GlobalCleanupAction = () => { },
        };

        // Act
        using var engine = new AllocationEngine(timing, null, null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(engine.Host).IsSameReferenceAs(timing.Host);
            await Assert.That(engine.TargetJob).IsSameReferenceAs(timing.TargetJob);
            await Assert.That(engine.OperationsPerInvoke).IsEqualTo(3);
            await Assert.That((object?)engine.GlobalSetupAction).IsSameReferenceAs(timing.GlobalSetupAction);
            await Assert.That((object?)engine.GlobalCleanupAction).IsSameReferenceAs(timing.GlobalCleanupAction);
            await Assert.That((object)engine.WorkloadAction).IsSameReferenceAs(timing.WorkloadAction);
            await Assert.That((object)engine.OverheadAction).IsSameReferenceAs(timing.OverheadAction);
            await Assert.That(engine.Resolver).IsSameReferenceAs(timing.Resolver);
        }
    }
}
