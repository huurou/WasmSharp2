using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_InitializeTests
{
    [Test]
    [Arguments("invoke.single")]
    [Arguments("memory.copy-fill")]
    public async Task 測定前の結果が固定期待値と異なる_対象ケースとInvoke段階を含む例外で中断する(
        string caseId
    )
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        var assets = InvokeFixture.CreateAssets(caseId, invalidResult: true);

        // Act & Assert
        var exception = await Assert
            .That(() => benchmark.Initialize(assets, caseId))
            .Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains($"{caseId}/Invoke/results[0]");
    }
}
