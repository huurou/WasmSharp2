using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_DecodeLargeTests
{
    private readonly WasmExecutionContext _context = new WasmExecutionContext();

    private readonly CaseJson _json = new CaseJson();
    private readonly MeasurementMethods _methods = new MeasurementMethods();

    [Test]
    public async Task 固定素材から反復する_毎回新しい未検証moduleを返す()
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        benchmark.Initialize(PreparationFixture.CreateAssets(_json, _methods), "decode.large", _json);

        // Act
        var first = benchmark.DecodeLarge();
        var second = benchmark.DecodeLarge();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(benchmark.Cleanup).ThrowsNothing();
            await Assert.That(() => second.Instantiate(_context, CaseVerifier.CreateImports())).ThrowsNothing();
            // Act & Assert
            await Assert
                .That(() => first.Instantiate(_context, CaseVerifier.CreateImports()))
                .Throws<InvalidOperationException>();
        }
    }
}
