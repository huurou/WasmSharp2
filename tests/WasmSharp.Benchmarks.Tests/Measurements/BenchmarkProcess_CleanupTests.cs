using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

[NotInParallel]
internal class BenchmarkProcess_CleanupTests
{
    [Test]
    public async Task 後検査成功後の保存が失敗_検査成功を保持して保存失敗を記録する()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");
        process.Setup((_, _) => { });

        // Act & Assert
        var exception = await Assert
            .That(() => process.Cleanup(fixture.BlockCheckWrites))
            .Throws<IOException>();
        fixture.RestoreCheckWrites();
        process.Cleanup(() => { });
        var failed = RunStore
            .ReadChecks(fixture.Run.DirectoryPath)
            .Single(x => x.Check.Failures.Count > 0)
            .Check;
        using (Assert.Multiple())
        {
            await Assert.That(failed.Precheck!.Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(failed.Postcheck!.Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(failed.ExecutionStarted).IsTrue();
            await Assert.That(failed.Failures.Single().Stage).IsEqualTo(RunStage.Saving);
            await Assert.That(failed.Failures.Single().Reason).IsNotEmpty();
            await Assert
                .That(failed.Failures.Single().LastConfirmedStage)
                .IsEqualTo(RunStage.Postcheck);
            await Assert
                .That(failed.Failures.Single().ExceptionType)
                .IsEqualTo(exception!.GetType().FullName);
        }
    }

    [Test]
    public async Task 後検査失敗の保存も失敗_元の検査例外を同じ実体で返す()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");
        process.Setup((_, _) => { });
        var expected = new InvalidOperationException("後検査の不一致");

        // Act & Assert
        var actual = await Assert
            .That(() =>
                process.Cleanup(() =>
                {
                    fixture.BlockCheckWrites();
                    throw expected;
                })
            )
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
    }

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
        var exception = await Assert
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
            var observation = failed.Postcheck!.Observations.Single();
            await Assert.That(observation.Name).IsNotEmpty();
            await Assert.That(observation.Expected).IsNotEmpty();
            await Assert.That(observation.Actual).IsEqualTo(exception!.Message);
            await Assert.That(observation.Matches).IsFalse();
            await Assert.That(failed.LastConfirmedStage).IsEqualTo(RunStage.ExecutionStarted);
            await Assert.That(failed.Failures.Single().Stage).IsEqualTo(RunStage.Postcheck);
            await Assert.That(failed.Failures.Single().Reason).IsNotEmpty();
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
