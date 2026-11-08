using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_ScanSimdTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 固定入力で反復する_最後の結果と状態が固定期待値に一致する()
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        benchmark.Initialize(InvokeFixture.CreateAssets("scan.simd", json_, methods_), "scan.simd", json_);

        // Act
        for (var iteration = 0; iteration < 8; iteration++)
        {
            benchmark.ScanSimd();
        }

        // Assert
        await Assert.That(benchmark.Cleanup).ThrowsNothing();
    }
}
