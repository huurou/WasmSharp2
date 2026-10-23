using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_CopyFillTests
{
    [Test]
    public async Task 固定入力で反復する_最後の結果と状態が固定期待値に一致する()
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        benchmark.Initialize(InvokeFixture.CreateAssets("memory.copy-fill"), "memory.copy-fill");

        // Act
        for (var iteration = 0; iteration < 8; iteration++)
        {
            benchmark.CopyFill();
        }

        // Assert
        await Assert.That(benchmark.Cleanup).ThrowsNothing();
    }
}
