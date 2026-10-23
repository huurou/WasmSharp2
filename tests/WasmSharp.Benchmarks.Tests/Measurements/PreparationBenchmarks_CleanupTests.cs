using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_CleanupTests
{
    [Test]
    [Arguments("memory")]
    [Arguments("start_result")]
    [Arguments("functions")]
    public async Task 最後のinstanceの初期状態が異なる_対象ケースと段階を含む例外で後検査を失敗させる(
        string exportName
    )
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        benchmark.Initialize(PreparationFixture.CreateAssets(), "instantiate.large");
        benchmark.InstantiateLarge();
        var last = benchmark.InstantiateLarge();
        switch (exportName)
        {
            case "memory":
                last.GetMemory(exportName).Write(7, new byte[] { 0xff });
                break;
            case "start_result":
                last.GetGlobalResource(exportName).Value = WasmValue.FromI32(-1);
                break;
            case "functions":
                last.GetTable(exportName).Set(0, WasmValue.FromFuncRef(null));
                break;
        }

        // Act & Assert
        var exception = await Assert.That(benchmark.Cleanup).Throws<InvalidOperationException>();
        await Assert
            .That(exception!.Message)
            .Contains("instantiate.large/Instantiate/")
            .And.Contains(exportName);
    }
}
