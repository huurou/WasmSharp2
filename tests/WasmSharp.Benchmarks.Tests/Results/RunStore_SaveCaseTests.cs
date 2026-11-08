using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_SaveCaseTests
{
    private readonly CaseJson json_ = new CaseJson();

    [Test]
    public async Task Manifest保存に失敗した後に保存先を復旧する_同じstoreは完了できない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);
        fixture.SaveConditions(store);
        var result = fixture.SaveSucceededCase(store);
        var manifestPath = Path.Combine(fixture.DirectoryPath, "run.json");
        var original = File.ReadAllBytes(manifestPath);
        File.Delete(manifestPath);
        Directory.CreateDirectory(manifestPath);

        // Act & Assert
        Exception? saveFailure;
        try
        {
            saveFailure = await Assert.That(() => store.SaveCase(result)).Throws<Exception>();
        }
        finally
        {
            Directory.Delete(manifestPath);
            File.WriteAllBytes(manifestPath, original);
        }
        var completionFailure = await Assert
            .That(() => store.Complete(ResultStatus.Succeeded))
            .ThrowsExactly<InvalidOperationException>();
        var actual = RunStore.Load(fixture.DirectoryPath, json_);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saveFailure is IOException or UnauthorizedAccessException).IsTrue();
            await Assert
                .That(completionFailure!.Message)
                .Contains("保存に失敗したrunは完了できません");
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(actual.CompletedAtUtc).IsNull();
        }
    }

    [Test]
    public async Task 呼出元が保存後に一覧を変更する_保存済みのケースとraw対応を変えない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);
        fixture.SaveConditions(store);
        var result = fixture.SaveSucceededCase(store);

        // Act
        result.RawFiles.Clear();
        result.BenchmarkReport!.Processes.Clear();
        store.Complete(ResultStatus.Succeeded);
        var actual = RunStore.Load(fixture.DirectoryPath, json_);

        // Assert
        await Assert.That(actual.Cases.Single().RawFiles.Count).IsEqualTo(2);
        await Assert.That(actual.Cases.Single().BenchmarkReport!.Processes.Count).IsEqualTo(1);
    }
}
