using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_LoadTests
{
    [Test]
    [Arguments("source/index.patch", false)]
    [Arguments("source/index.patch", true)]
    [Arguments("source/worktree.patch", false)]
    [Arguments("source/worktree.patch", true)]
    [Arguments("tools/WasmSharp.Benchmarks/New.cs", false)]
    [Arguments("tools/WasmSharp.Benchmarks/New.cs", true)]
    public async Task 完了後にsourceの保存参照が欠落または変更される_未完了として読み込む(
        string relativePath,
        bool changed
    )
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        fixture.SaveSucceededCase(store);
        store.Complete(ResultStatus.Succeeded);
        var path = Path.Combine(fixture.DirectoryPath, relativePath);
        if (changed)
        {
            File.WriteAllText(path, "保存後の変更");
        }
        else
        {
            File.Delete(path);
        }

        // Act
        var actual = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(actual.CompletedAtUtc).IsNull();
            await Assert
                .That(actual.Failures.Single().InterruptionReason)
                .Contains("保存参照の照合に失敗");
        }
    }

    [Test]
    [Arguments("environment.json")]
    [Arguments("raw/full.json")]
    public async Task 完了記録があっても保存参照が壊れている_未完了として読込む(string relativePath)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        fixture.SaveSucceededCase(store);
        store.Complete(ResultStatus.Succeeded);
        File.WriteAllText(Path.Combine(fixture.DirectoryPath, relativePath), "破損");

        // Act
        var actual = RunStore.Load(fixture.DirectoryPath);

        // Assert
        await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
        await Assert.That(actual.CompletedAtUtc).IsNull();
        await Assert
            .That(actual.Failures.Single().InterruptionReason)
            .Contains("保存参照の照合に失敗");
    }

    [Test]
    public async Task 完了記録がない_成功状態や存在するrawから完了へ補完しない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        Directory.CreateDirectory(fixture.DirectoryPath);
        File.WriteAllBytes(
            Path.Combine(fixture.DirectoryPath, "run.json"),
            JsonSerializer.SerializeToUtf8Bytes(
                fixture.Manifest with
                {
                    CompletedAtUtc = null,
                },
                CaseJson.Options
            )
        );

        // Act
        var actual = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(actual.CompletedAtUtc).IsNull();
            await Assert.That(actual.Cases.Single().Metrics.Time).IsNotNull();
            await Assert.That(actual.Failures.Any(x => x.InterruptionReason is not null)).IsTrue();
        }
    }

    [Test]
    public async Task 前検査後に中断する_検査を保持してrunは未完了のまま読み込む()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        var precheck = fixture.Check with
        {
            ExecutionStarted = false,
            Postcheck = null,
            LastConfirmedStage = RunStage.Precheck,
        };

        // Act
        var file = store.SaveCheck(precheck);
        var actual = RunStore.Load(fixture.DirectoryPath);
        var saved = JsonSerializer.Deserialize<ProcessCheck>(
            File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, file.RelativePath)),
            CaseJson.Options
        )!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(actual.CompletedAtUtc).IsNull();
            await Assert.That(saved.RunId).IsEqualTo(actual.RunId);
            await Assert.That(saved.CaseId).IsEqualTo("invoke.single");
            await Assert.That(saved.Precheck!.Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(saved.Postcheck).IsNull();
        }
    }
}
