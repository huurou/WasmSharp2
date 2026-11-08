using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_InitializeTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 測定前の固定期待値が異なる_対象ケースと段階を含む例外で中断する()
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        var assets = PreparationFixture.CreateAssets(json_, methods_, invalidStart: true);

        // Act & Assert
        var exception = await Assert
            .That(() => benchmark.Initialize(assets, "instantiate.large", json_))
            .Throws<InvalidOperationException>();
        await Assert
            .That(exception!.Message)
            .Contains("instantiate.large/Instantiate/global.start_result")
            .And.Contains("期待=I32:ffffffff")
            .And.Contains("観測=I32:0000002a");
    }
}
