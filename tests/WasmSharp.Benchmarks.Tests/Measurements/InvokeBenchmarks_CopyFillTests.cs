using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_CopyFillTests
{
    private readonly CaseJson _json = new CaseJson();
    private readonly MeasurementMethods _methods = new MeasurementMethods();

    [Test]
    public async Task 固定入力で反復する_最後の結果と状態が固定期待値に一致する()
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        benchmark.Initialize(InvokeFixture.CreateAssets("memory.copy-fill", _json, _methods), "memory.copy-fill", _json);

        // Act
        for (var iteration = 0; iteration < 8; iteration++)
        {
            benchmark.CopyFill();
        }

        // Assert
        await Assert.That(benchmark.Cleanup).ThrowsNothing();
    }
}
