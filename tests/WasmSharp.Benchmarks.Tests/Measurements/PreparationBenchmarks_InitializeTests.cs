using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_InitializeTests
{
    [Test]
    public async Task 測定前の固定期待値が異なる_対象ケースと段階を含む例外で中断する()
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        var assets = PreparationFixture.CreateAssets(invalidStart: true);

        // Act & Assert
        var exception = await Assert
            .That(() => benchmark.Initialize(assets, "instantiate.large"))
            .Throws<InvalidOperationException>();
        await Assert
            .That(exception!.Message)
            .Contains("instantiate.large/Instantiate/global.start_result")
            .And.Contains("期待=I32:ffffffff")
            .And.Contains("観測=I32:0000002a");
    }
}
