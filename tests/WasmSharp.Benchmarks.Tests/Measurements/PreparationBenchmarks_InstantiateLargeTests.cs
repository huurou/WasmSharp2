using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class PreparationBenchmarks_InstantiateLargeTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 先行instanceを変更して反復する_資源とsegmentとstartを新しいinstanceで初期化する()
    {
        // Arrange
        var benchmark = new PreparationBenchmarks();
        benchmark.Initialize(PreparationFixture.CreateAssets(json_, methods_), "instantiate.large", json_);
        var first = benchmark.InstantiateLarge();
        first.GetMemory("memory").Write(0, [0xff]);
        first.GetTable("functions").Set(0, WasmValue.FromFuncRef(null));
        first.GetGlobalResource("start_result").Value = WasmValue.FromI32(-1);

        // Act
        var second = benchmark.InstantiateLarge();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert
                .That(ReferenceEquals(first.GetMemory("memory"), second.GetMemory("memory")))
                .IsFalse();
            await Assert
                .That(ReferenceEquals(first.GetTable("functions"), second.GetTable("functions")))
                .IsFalse();
            await Assert
                .That(
                    ReferenceEquals(
                        first.GetGlobalResource("start_result"),
                        second.GetGlobalResource("start_result")
                    )
                )
                .IsFalse();
            await Assert
                .That(ReferenceEquals(first.GetFunction("add_one"), second.GetFunction("add_one")))
                .IsTrue();
            await Assert.That(second.GetGlobal("start_result").AsI32()).IsEqualTo(42);
            await Assert.That(benchmark.Cleanup).ThrowsNothing();
        }
    }
}
