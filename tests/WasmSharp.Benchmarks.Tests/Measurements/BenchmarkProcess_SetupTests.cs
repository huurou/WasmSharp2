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
    public async Task 前検査で結果が不一致_実行を開始せずケースと例外を保存する()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        var benchmark = new InvokeBenchmarks();
        var process = BenchmarkProcess.Start("InvokeBenchmarks.Single");

        // Act & Assert
        await Assert
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
            await Assert.That(failed.ExecutionStarted).IsFalse();
            await Assert.That(failed.Failures.Single().CaseId).IsEqualTo("invoke.single");
            await Assert.That(failed.Failures.Single().Stage).IsEqualTo(RunStage.Precheck);
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
            using (Assert.Multiple())
            {
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
