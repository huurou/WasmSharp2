using BenchmarkDotNet.Engines;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class AllocationEngine_RunIterationTests
{
    [Test]
    public async Task 単一反復を要求する_反復条件を時間計測engineへ渡し同じ測定値を返す()
    {
        // Arrange
        var timing = new AllocationEngineFixture();
        using var engine = new AllocationEngine(timing, null, null);
        var data = new IterationData(IterationMode.Overhead, IterationStage.Warmup, 3, 12, 4);

        // Act
        var actual = engine.RunIteration(data);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(timing.LastIteration).IsEqualTo(data);
            await Assert.That(actual).IsEqualTo(timing.IterationResult);
            await Assert.That(timing.RunCount).IsEqualTo(0);
        }
    }
}
