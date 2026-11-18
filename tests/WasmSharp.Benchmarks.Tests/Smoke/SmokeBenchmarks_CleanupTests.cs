using WasmSharp.Benchmarks.Smoke;

namespace WasmSharp.Benchmarks.Tests.Smoke;

internal class SmokeBenchmarks_CleanupTests
{
    private readonly WasmExecutionContext context_ = new WasmExecutionContext();

    [Test]
    public async Task 最後のデコード結果を後検査する_最新moduleだけがインスタンス化できる状態になる()
    {
        // Arrange
        var benchmark = new SmokeBenchmarks();
        benchmark.Setup();
        var first = benchmark.Decode();
        var last = benchmark.Decode();

        // Act
        benchmark.Cleanup();

        // Assert
        await Assert.That(() => last.Instantiate(context_, new WasmImports())).ThrowsNothing();
        // Act & Assert
        await Assert.That(() => first.Instantiate(context_, new WasmImports())).ThrowsExactly<InvalidOperationException>();
    }
}
