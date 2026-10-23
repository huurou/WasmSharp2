using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

[NotInParallel]
internal class BenchmarkProcess_CleanupTests
{
    [Test]
    public async Task 最後の結果が不一致_後検査失敗と最後に確認した段階を保存して例外を返す()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var benchmark = new InvokeBenchmarks();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");
        process.Setup((_, id) => benchmark.Initialize(InvokeFixture.CreateDriftingAssets(), id));
        benchmark.Single();
        benchmark.Single();

        // Act & Assert
        await Assert
            .That(() => process.Cleanup(benchmark.Cleanup))
            .Throws<InvalidOperationException>();
        var failed = RunStore
            .ReadChecks(fixture.Run.DirectoryPath)
            .Single(x => x.Check.Postcheck is not null)
            .Check;
        using (Assert.Multiple())
        {
            await Assert.That(failed.Precheck!.Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(failed.Postcheck!.Status).IsEqualTo(ResultStatus.Failed);
            await Assert.That(failed.LastConfirmedStage).IsEqualTo(RunStage.ExecutionStarted);
            await Assert.That(failed.Failures.Single().Stage).IsEqualTo(RunStage.Postcheck);
            await Assert.That(failed.Failures.Single().Message!).Contains("観測=I32:00000001");
        }
    }

    [Test]
    public async Task 後検査へ到達しない_実行開始のsnapshotを未完了のまま残す()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var benchmark = new InvokeBenchmarks();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");

        // Act
        process.Setup(benchmark.Initialize);
        benchmark.Single();
        var checks = RunStore.ReadChecks(fixture.Run.DirectoryPath).Select(x => x.Check).ToList();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(checks.Any(x => x.ExecutionStarted)).IsTrue();
            await Assert.That(checks.All(x => x.Postcheck is null)).IsTrue();
            await Assert
                .That(RunStore.Load(fixture.Run.DirectoryPath).Status)
                .IsEqualTo(ResultStatus.Incomplete);
        }
    }
}
