using WasmSharp.Benchmarks.Smoke;

namespace WasmSharp.Benchmarks.Tests.Smoke;

internal class SmokeBenchmarks_DecodeTests
{
    private readonly WasmExecutionContext context_ = new WasmExecutionContext();

    [Test]
    public async Task 空moduleのデコードを反復する_毎回新しい未検証moduleを返す()
    {
        // Arrange
        var benchmark = new SmokeBenchmarks();
        benchmark.Setup();

        // Act
        var first = benchmark.Decode();
        var second = benchmark.Decode();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(first.GetExports()).IsEmpty();
            await Assert.That(second.GetExports()).IsEmpty();
            // Act & Assert
            await Assert
                .That(() => first.Instantiate(context_, new WasmImports()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert
                .That(() => second.Instantiate(context_, new WasmImports()))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
