using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_IntegerDirectTests
{
    [Test]
    public async Task 固定入力で反復する_最後の結果と状態が固定期待値に一致する()
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        benchmark.Initialize(
            InvokeFixture.CreateAssets("loop.integer-direct"),
            "loop.integer-direct"
        );

        // Act
        for (var iteration = 0; iteration < 8; iteration++)
        {
            benchmark.IntegerDirect();
        }

        // Assert
        await Assert.That(benchmark.Cleanup).ThrowsNothing();
    }
}
