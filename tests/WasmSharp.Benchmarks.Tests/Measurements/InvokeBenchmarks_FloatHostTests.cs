using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_FloatHostTests
{
    [Test]
    public async Task 固定入力で反復する_最後の結果と状態が固定期待値に一致する()
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        benchmark.Initialize(InvokeFixture.CreateAssets("loop.float-host"), "loop.float-host");

        // Act
        for (var iteration = 0; iteration < 8; iteration++)
        {
            benchmark.FloatHost();
        }

        // Assert
        await Assert.That(benchmark.Cleanup).ThrowsNothing();
    }
}
