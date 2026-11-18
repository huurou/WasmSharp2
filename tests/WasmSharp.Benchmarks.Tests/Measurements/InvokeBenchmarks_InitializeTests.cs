using System.Collections.Immutable;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class InvokeBenchmarks_InitializeTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 固定入力がdataセグメントと異なる_検査用と測定用の両instanceへ同じ入力を書き込む()
    {
        // Arrange
        var definition = VerificationFixture.ChangedMemoryInputDefinition(MeasurementStage.Invoke, json_);
        var bytes = File.ReadAllBytes(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "Verification", "state.wasm")
            )
            .ToImmutableArray();
        var assets = InvokeFixture.BuildAssets(definition, bytes, json_, methods_);
        var benchmark = new InvokeBenchmarks();

        // Act
        benchmark.Initialize(assets, definition.CaseId, json_);
        benchmark.Single();

        // Assert
        await Assert.That(benchmark.Cleanup).ThrowsNothing();
    }

    [Test]
    [Arguments("invoke.single")]
    [Arguments("memory.copy-fill")]
    public async Task 測定前の結果が固定期待値と異なる_対象ケースとInvoke段階を含む例外で中断する(string caseId)
    {
        // Arrange
        var benchmark = new InvokeBenchmarks();
        var assets = InvokeFixture.CreateAssets(caseId, json_, methods_, invalidResult: true);

        // Act & Assert
        var exception = await Assert
            .That(() => benchmark.Initialize(assets, caseId, json_))
            .Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains($"{caseId}/Invoke/results[0]");
    }
}
