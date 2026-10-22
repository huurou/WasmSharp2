using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_CompleteTests
{
    [Test]
    [Arguments("source/index.patch", false)]
    [Arguments("source/index.patch", true)]
    [Arguments("source/worktree.patch", false)]
    [Arguments("source/worktree.patch", true)]
    [Arguments("tools/WasmSharp.Benchmarks/New.cs", false)]
    [Arguments("tools/WasmSharp.Benchmarks/New.cs", true)]
    public async Task sourceの保存参照が欠落または変更される_完了を拒否して未完了を保持する(
        string relativePath,
        bool changed
    )
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        fixture.SaveSucceededCase(store);
        var path = Path.Combine(fixture.DirectoryPath, relativePath);
        if (changed)
        {
            File.WriteAllText(path, "保存後の変更");
        }
        else
        {
            File.Delete(path);
        }

        // Act & Assert
        await Assert.That(() => store.Complete(ResultStatus.Succeeded)).Throws<IOException>();
        var actual = RunStore.Load(fixture.DirectoryPath);
        await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
        await Assert.That(actual.CompletedAtUtc).IsNull();
    }

    [Test]
    [Arguments(ResultStatus.Failed)]
    [Arguments(ResultStatus.Unavailable)]
    public async Task 指標を取得できなかったケースを保存する_状態とnullと理由を保持する(
        ResultStatus status
    )
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        var failure = new RunFailure(
            "invoke.single",
            RunStage.BenchmarkProcessExit,
            RunStage.Precheck,
            "測定を取得できません。",
            null,
            null,
            "process中断"
        );
        store.SaveCase(
            new(
                "invoke.single",
                status,
                new(null, "時間未取得", null, "割当未取得", null, "GC未取得"),
                null,
                [failure],
                []
            )
        );

        // Act
        store.Complete(status, [failure]);
        var actual = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Status).IsEqualTo(status);
            await Assert.That(actual.Cases.Single().Status).IsEqualTo(status);
            await Assert.That(actual.Cases.Single().Metrics.Time).IsNull();
            await Assert
                .That(actual.Cases.Single().Metrics.TimeUnavailableReason)
                .IsEqualTo("時間未取得");
            await Assert.That(actual.Cases.Single().Metrics.Allocation).IsNull();
            await Assert
                .That(actual.Cases.Single().Metrics.AllocationUnavailableReason)
                .IsEqualTo("割当未取得");
            await Assert.That(actual.Cases.Single().Metrics.Gc).IsNull();
            await Assert
                .That(actual.Cases.Single().Metrics.GcUnavailableReason)
                .IsEqualTo("GC未取得");
            await Assert.That(actual.Failures).IsEquivalentTo([failure]);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 保存したrawが欠落または変更される_完了せず未完了を保持する(bool changed)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        var result = fixture.SaveSucceededCase(store);
        var rawPath = Path.Combine(fixture.DirectoryPath, result.RawFiles[0].RelativePath);
        if (changed)
        {
            File.WriteAllText(rawPath, "別のraw");
        }
        else
        {
            File.Delete(rawPath);
        }

        // Act & Assert
        await Assert.That(() => store.Complete(ResultStatus.Succeeded)).Throws<IOException>();
        await Assert
            .That(RunStore.Load(fixture.DirectoryPath).Status)
            .IsEqualTo(ResultStatus.Incomplete);
    }

    [Test]
    public async Task 結果と検査のprocessが異なる_対応しない検査を完了へ採用しない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        var check = store.SaveCheck(fixture.Check with { ProcessId = 9999 });
        var result = fixture.Manifest.Cases.Single() with
        {
            Metrics = new(null, "未取得", null, "未取得", null, "未取得"),
            RawFiles = [],
            BenchmarkReport = fixture.Manifest.Cases.Single().BenchmarkReport! with
            {
                Processes =
                [
                    fixture.Manifest.Cases.Single().BenchmarkReport!.Processes.Single() with
                    {
                        CheckFile = check,
                    },
                ],
            },
        };
        store.SaveCase(result);

        // Act & Assert
        await Assert
            .That(() => store.Complete(ResultStatus.Succeeded))
            .ThrowsExactly<InvalidDataException>();
        await Assert
            .That(RunStore.Load(fixture.DirectoryPath).Status)
            .IsEqualTo(ResultStatus.Incomplete);
    }

    [Test]
    public async Task 完了後に追記する_確定したrunを変更しない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        var result = fixture.SaveSucceededCase(store);
        store.Complete(ResultStatus.Succeeded);
        var original = File.ReadAllText(Path.Combine(fixture.DirectoryPath, "run.json"));

        // Act & Assert
        await Assert
            .That(() => store.SaveFile("raw/late.txt", "late"u8))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => store.SaveCase(result)).ThrowsExactly<InvalidOperationException>();
        await Assert
            .That(() => store.Complete(ResultStatus.Failed))
            .ThrowsExactly<InvalidOperationException>();
        await Assert
            .That(File.ReadAllText(Path.Combine(fixture.DirectoryPath, "run.json")))
            .IsEqualTo(original);
    }

    [Test]
    public async Task 条件が未保存である_完了を拒否して未完了を保持する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveSucceededCase(store);

        // Act & Assert
        await Assert
            .That(() => store.Complete(ResultStatus.Succeeded))
            .ThrowsExactly<InvalidOperationException>();
        await Assert
            .That(RunStore.Load(fixture.DirectoryPath).Status)
            .IsEqualTo(ResultStatus.Incomplete);
    }

    [Test]
    public async Task 保存に一度失敗する_再試行で完了へ変更せず未完了を保持する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        fixture.SaveSucceededCase(store);

        // Act & Assert
        await Assert
            .That(() => store.SaveFile("raw/full.json", "上書き"u8))
            .ThrowsExactly<IOException>();
        await Assert
            .That(() => store.Complete(ResultStatus.Succeeded))
            .ThrowsExactly<InvalidOperationException>();
        var actual = RunStore.Load(fixture.DirectoryPath);
        await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
        await Assert.That(actual.CompletedAtUtc).IsNull();
        await Assert
            .That(File.ReadAllText(Path.Combine(fixture.DirectoryPath, "raw", "full.json")))
            .IsEqualTo("{\"case\":\"invoke.single\"}");
    }

    [Test]
    public async Task 最後の保存に失敗する_完了状態を途中まで書かず未完了を再読込できる()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        fixture.SaveSucceededCase(store);

        if (OperatingSystem.IsWindows())
        {
            using var locked = new FileStream(
                Path.Combine(fixture.DirectoryPath, "run.json"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None
            );

            // Act & Assert
            await Assert
                .That(() => store.Complete(ResultStatus.Succeeded))
                .ThrowsExactly<UnauthorizedAccessException>();
        }
        else
        {
            var permissions = File.GetUnixFileMode(fixture.DirectoryPath);
            try
            {
                File.SetUnixFileMode(
                    fixture.DirectoryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserExecute
                );

                // Act & Assert
                await Assert
                    .That(() => store.Complete(ResultStatus.Succeeded))
                    .ThrowsExactly<UnauthorizedAccessException>();
            }
            finally
            {
                File.SetUnixFileMode(fixture.DirectoryPath, permissions);
            }
        }

        var actual = RunStore.Load(fixture.DirectoryPath);
        await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
        await Assert.That(actual.CompletedAtUtc).IsNull();
    }

    [Test]
    public async Task 予定ケースが未保存である_完了を拒否して未完了のまま保持する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);

        // Act & Assert
        await Assert
            .That(() => store.Complete(ResultStatus.Succeeded))
            .ThrowsExactly<InvalidOperationException>();
        var actual = RunStore.Load(fixture.DirectoryPath);
        await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
        await Assert.That(actual.CompletedAtUtc).IsNull();
    }

    [Test]
    public async Task 条件とケース結果を保存して完了する_同じrunのrawとprocess検査を再読込できる()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        var raw = store.SaveFile("raw/full.json", "{\"case\":\"invoke.single\"}"u8);
        var measurements = store.SaveFile("raw/measurements.csv", "operation,ns\n1,125.25"u8);
        var check = store.SaveCheck(fixture.Check);
        var result = fixture.Manifest.Cases.Single();
        result = result with
        {
            RawFiles = [raw, measurements],
            Metrics = result.Metrics with
            {
                Time = result.Metrics.Time! with { RawMeasurements = measurements },
            },
            BenchmarkReport = result.BenchmarkReport! with
            {
                Processes = [result.BenchmarkReport.Processes.Single() with { CheckFile = check }],
            },
        };
        store.SaveCase(result);

        // Act
        store.Complete(ResultStatus.Succeeded);
        var actual = RunStore.Load(fixture.DirectoryPath);
        var restoredCheck = JsonSerializer.Deserialize<ProcessCheck>(
            File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, check.RelativePath)),
            CaseJson.Options
        )!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(actual.CompletedAtUtc).IsNotNull();
            await Assert.That(actual.Cases.Single().CaseId).IsEqualTo("invoke.single");
            await Assert.That(actual.Cases.Single().RawFiles).IsEquivalentTo([raw, measurements]);
            await Assert
                .That(actual.Cases.Single().Metrics.Time!.RawMeasurements)
                .IsEqualTo(measurements);
            await Assert
                .That(actual.Cases.Single().BenchmarkReport!.Processes.Single().CheckFile)
                .IsEqualTo(check);
            await Assert
                .That(raw.Sha256)
                .IsEqualTo("edeae6278fcd26ef2a3edede5984e0965edb505c4b73152650e79d9762f7711f");
            await Assert.That(restoredCheck.RunId).IsEqualTo(actual.RunId);
            await Assert.That(restoredCheck.CaseId).IsEqualTo("invoke.single");
            await Assert.That(restoredCheck.JobId).IsEqualTo("job-1");
            await Assert.That(restoredCheck.ProcessId).IsEqualTo(4321);
            await Assert.That(actual.EnvironmentFile!.RelativePath).IsEqualTo("environment.json");
            await Assert.That(actual.SourceFile!.RelativePath.StartsWith("source/")).IsTrue();
            await Assert
                .That(actual.AssetManifestFile!.RelativePath)
                .IsEqualTo("assets/asset-manifest.json");
            await Assert.That(actual.CasesFile!.RelativePath).IsEqualTo("cases.json");
            await Assert.That(actual.JobSettingsFile!.RelativePath).IsEqualTo("job.json");
            await Assert
                .That(File.ReadAllText(Path.Combine(fixture.DirectoryPath, raw.RelativePath)))
                .IsEqualTo("{\"case\":\"invoke.single\"}");
            await Assert.That(actual.Kind).IsEqualTo(RunKind.Measurement);
        }
    }
}
