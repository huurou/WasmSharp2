using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_DecodeValidateSmallTests
{
    private readonly WasmExecutionContext context_ = new WasmExecutionContext();

    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 固定素材から反復する_毎回新しい検証済みmoduleを返す()
    {
        // Arrange
        var assets = PreparationFixture.CreateAssets(json_, methods_);
        var benchmark = new PreparationBenchmarks();
        benchmark.Initialize(assets, "decode-validate.small", json_);

        // Act
        var first = benchmark.DecodeValidateSmall();
        var second = benchmark.DecodeValidateSmall();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert
                .That(() => first.Instantiate(context_, CaseVerifier.CreateImports()))
                .ThrowsNothing();
            await Assert.That(benchmark.Cleanup).ThrowsNothing();
        }
    }
}
