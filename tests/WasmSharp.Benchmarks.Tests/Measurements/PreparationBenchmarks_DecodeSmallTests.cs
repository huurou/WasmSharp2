using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_DecodeSmallTests
{
    private readonly WasmExecutionContext context_ = new WasmExecutionContext();

    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 固定素材から反復する_毎回新しい未検証moduleを返す()
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        benchmark.Initialize(PreparationFixture.CreateAssets(json_, methods_), "decode.small", json_);

        // Act
        var first = benchmark.DecodeSmall();
        var second = benchmark.DecodeSmall();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(benchmark.Cleanup).ThrowsNothing();
            await Assert
                .That(() => second.Instantiate(context_, CaseVerifier.CreateImports()))
                .ThrowsNothing();
            // Act & Assert
            await Assert
                .That(() => first.Instantiate(context_, CaseVerifier.CreateImports()))
                .Throws<InvalidOperationException>();
        }
    }
}
