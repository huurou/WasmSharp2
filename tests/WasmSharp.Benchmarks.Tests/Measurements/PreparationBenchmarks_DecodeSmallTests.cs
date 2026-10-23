using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_DecodeSmallTests
{
    [Test]
    public async Task 固定素材から反復する_毎回新しい未検証moduleを返す()
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        benchmark.Initialize(PreparationFixture.CreateAssets(), "decode.small");

        // Act
        var first = benchmark.DecodeSmall();
        var second = benchmark.DecodeSmall();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(benchmark.Cleanup).ThrowsNothing();
            await Assert
                .That(() => second.Instantiate(CaseVerifier.CreateImports()))
                .ThrowsNothing();
            // Act & Assert
            await Assert
                .That(() => first.Instantiate(CaseVerifier.CreateImports()))
                .Throws<InvalidOperationException>();
        }
    }
}
