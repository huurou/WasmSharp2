using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

[NotInParallel]
internal class BenchmarkProcess_SetupTests
{
    [Test]
    public async Task 前検査成功後の保存が失敗_検査成功と未開始を保持して保存失敗を記録する()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");

        // Act & Assert
        var exception = await Assert
            .That(() => process.Setup((_, _) => fixture.BlockCheckWrites()))
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
            await Assert.That(failed.ExecutionStarted).IsFalse();
            await Assert.That(failed.Failures.Single().Stage).IsEqualTo(RunStage.Saving);
            await Assert.That(failed.Failures.Single().Reason).IsNotEmpty();
            await Assert
                .That(failed.Failures.Single().LastConfirmedStage)
                .IsEqualTo(RunStage.Precheck);
            await Assert
                .That(failed.Failures.Single().ExceptionType)
                .IsEqualTo(exception!.GetType().FullName);
        }
    }

    [Test]
    public async Task 前検査失敗の保存も失敗_元の検査例外を同じ実体で返す()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");
        var expected = new InvalidOperationException("前検査の不一致");

        // Act & Assert
        var actual = await Assert
            .That(() =>
                process.Setup(
                    (_, _) =>
                    {
                        fixture.BlockCheckWrites();
                        throw expected;
                    }
                )
            )
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task 前検査で結果が不一致_実行を開始せずケースと例外を保存する()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var benchmark = new InvokeBenchmarks();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");

        // Act & Assert
        var exception = await Assert
            .That(() =>
                process.Setup(
                    (_, id) =>
                        benchmark.Initialize(
                            InvokeFixture.CreateAssets("invoke.single", invalidResult: true),
                            id
                        )
                )
            )
            .Throws<InvalidOperationException>();
        var failed = RunStore
            .ReadChecks(fixture.Run.DirectoryPath)
            .Single(x => x.Check.Failures.Count > 0)
            .Check;
        using (Assert.Multiple())
        {
            await Assert.That(failed.Precheck!.Status).IsEqualTo(ResultStatus.Failed);
            var observation = failed.Precheck!.Observations.Single();
            await Assert.That(observation.Name).IsNotEmpty();
            await Assert.That(observation.Expected).IsNotEmpty();
            await Assert.That(observation.Actual).IsEqualTo(exception!.Message);
            await Assert.That(observation.Matches).IsFalse();
            await Assert.That(failed.Postcheck).IsNull();
            await Assert.That(failed.ExecutionStarted).IsFalse();
            await Assert.That(failed.Failures.Single().CaseId).IsEqualTo("invoke.single");
            await Assert.That(failed.Failures.Single().Stage).IsEqualTo(RunStage.Precheck);
            await Assert.That(failed.Failures.Single().Reason).IsNotEmpty();
            await Assert
                .That(failed.Failures.Single().ExceptionType)
                .IsEqualTo(typeof(InvalidOperationException).FullName);
        }
    }

    [Test]
    public async Task 設定から測定を準備する_前後検査と実行開始を同じprocessへ保存する()
    {
        // Arrange
        using var assets = new AssetLoadingFixture();
        using var run = new RunStoreFixture();
        RunStore.Create(run.DirectoryPath, run.Manifest);
        var configuration = new RunConfiguration(
            run.Manifest.RunId,
            run.DirectoryPath,
            assets.ManifestPath,
            "SmokeJob",
            ["invoke.single"]
        );
        var path = Path.Combine(run.DirectoryPath, "config.json");
        File.WriteAllBytes(
            path,
            JsonSerializer.SerializeToUtf8Bytes(configuration, CaseJson.Options)
        );
        var previous = Environment.GetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE);
        Environment.SetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE, path);
        try
        {
            var benchmark = new InvokeBenchmarks();

            // Act
            benchmark.SetupSingle();
            benchmark.Single();
            benchmark.Cleanup();

            // Assert
            var checks = RunStore.ReadChecks(run.DirectoryPath).Select(x => x.Check).ToList();
            var prechecked = checks.Single(x =>
                x.Precheck?.Status == ResultStatus.Succeeded && !x.ExecutionStarted
            );
            var completed = checks.Single(x => x.Postcheck is not null);
            using (Assert.Multiple())
            {
                await Assert.That(prechecked.LastConfirmedStage).IsEqualTo(RunStage.Precheck);
                await Assert.That(prechecked.Postcheck).IsNull();
                await Assert.That(prechecked.AffinityAfter).IsNull();
                foreach (var check in new[] { prechecked.Precheck!, completed.Postcheck! })
                {
                    var observation = check.Observations.Single();
                    await Assert.That(observation.Name).IsNotEmpty();
                    await Assert.That(observation.Expected).IsNotEmpty();
                    await Assert.That(observation.Actual).IsEqualTo(observation.Expected);
                    await Assert.That(observation.Matches).IsTrue();
                }
                await Assert
                    .That(checks.Any(x => x.Precheck?.Status == ResultStatus.Succeeded))
                    .IsTrue();
                await Assert.That(checks.Any(x => x.ExecutionStarted)).IsTrue();
                await Assert
                    .That(checks.Any(x => x.Postcheck?.Status == ResultStatus.Succeeded))
                    .IsTrue();
                await Assert
                    .That(
                        checks.All(x =>
                            x.RunId == run.Manifest.RunId
                            && x.CaseId == "invoke.single"
                            && x.JobId == "SmokeJob"
                            && x.ProcessId == Environment.ProcessId
                        )
                    )
                    .IsTrue();
                await Assert.That(checks.Last().LoadedAssemblies.Count).IsEqualTo(2);
                await Assert
                    .That(
                        checks.All(x =>
                            x.RuntimeVersion == EnvironmentCapture.CurrentRuntimeVersion
                        )
                    )
                    .IsTrue();
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    using var current = Process.GetCurrentProcess();
                    await Assert
                        .That(completed.AffinityBefore)
                        .IsEqualTo(current.ProcessorAffinity.ToInt64());
                    await Assert
                        .That(completed.AffinityAfter)
                        .IsEqualTo(current.ProcessorAffinity.ToInt64());
                    await Assert.That(completed.AffinityUnavailableReason).IsNull();
                }
                await Assert
                    .That(
                        checks
                            .Last()
                            .LoadedAssemblies.All(x =>
                                Path.IsPathFullyQualified(x.Path)
                                && Convert.ToHexStringLower(
                                    SHA256.HashData(File.ReadAllBytes(x.Path))
                                ) == x.Sha256
                            )
                    )
                    .IsTrue();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE, previous);
        }
    }
}
