using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_CleanupTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    [Arguments(StatePolicy.ReadOnly)]
    [Arguments(StatePolicy.FixedPoint)]
    public async Task 最後の結果が固定期待値と異なる_追加Invokeせず保持した不一致を返す(StatePolicy statePolicy)
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        var original = InvokeFixture.CreateDriftingAssets(json_, methods_);
        var definition = original.Catalog.Get("invoke.single");
        var assets = InvokeFixture.BuildAssets(
            definition with
            {
                StatePolicy = statePolicy,
            },
            original.GetBinary(definition.AssetId),
            json_,
            methods_
        );
        benchmark.Initialize(assets, "invoke.single", json_);
        if (statePolicy == StatePolicy.ReadOnly)
        {
            benchmark.Single();
        }
        benchmark.Single();

        // Act & Assert
        var first = await Assert.That(benchmark.Cleanup).Throws<InvalidOperationException>();
        var second = await Assert.That(benchmark.Cleanup).Throws<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(first!.Message)
                .Contains("invoke.single/Invoke/results[0]")
                .And.Contains("期待=I32:00000000, 観測=I32:00000001");
            await Assert.That(second!.Message).IsEqualTo(first.Message);
        }
    }
}
