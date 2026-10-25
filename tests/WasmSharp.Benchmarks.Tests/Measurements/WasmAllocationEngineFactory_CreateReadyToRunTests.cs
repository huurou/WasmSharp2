using BenchmarkDotNet.Engines;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class WasmAllocationEngineFactory_CreateReadyToRunTests
{
    [Test]
    public async Task 追加統計が不要_標準エンジンをそのまま返す()
    {
        // Arrange
        var timing = new AllocationEngineFixture();
        var parameters = new EngineParameters { MeasureExtraStats = false };

        // Act
        using var engine = new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(engine).IsSameReferenceAs(timing);
            await Assert.That(timing.RequestedExtraStats).IsFalse();
            await Assert.That(parameters.MeasureExtraStats).IsFalse();
        }
    }

    [Test]
    public async Task 標準factoryが失敗する_要求値を復元して同じ例外を伝播する()
    {
        // Arrange
        var expected = new InvalidOperationException("セットアップ失敗");
        var timing = new AllocationEngineFixture { CreationFailure = expected };
        var parameters = new EngineParameters { MeasureExtraStats = true };

        // Act & Assert
        var actual = await Assert
            .That(() => new WasmAllocationEngineFactory(timing).CreateReadyToRun(parameters))
            .ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsSameReferenceAs(expected);
            await Assert.That(parameters.MeasureExtraStats).IsTrue();
        }
    }

    [Test]
    public async Task 追加統計を要求する_標準factoryには時間計測だけを委譲して要求値を保持する()
    {
        // Arrange
        var timing = new AllocationEngineFixture();
        var parameters = new EngineParameters { MeasureExtraStats = true };
        var factory = new WasmAllocationEngineFactory(timing);

        // Act
        using var engine = factory.CreateReadyToRun(parameters);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(timing.RequestedExtraStats).IsFalse();
            await Assert.That(parameters.MeasureExtraStats).IsTrue();
            await Assert.That(timing.RunCount).IsEqualTo(0);
        }
    }
}
