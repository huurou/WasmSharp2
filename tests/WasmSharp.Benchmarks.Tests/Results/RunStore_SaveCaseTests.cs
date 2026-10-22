using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_SaveCaseTests
{
    [Test]
    public async Task 呼出元が保存後に一覧を変更する_保存済みのケースとraw対応を変えない()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        fixture.SaveConditions(store);
        var result = fixture.SaveSucceededCase(store);

        // Act
        result.RawFiles.Clear();
        result.BenchmarkReport!.Processes.Clear();
        store.Complete(ResultStatus.Succeeded);
        var actual = RunStore.Load(fixture.DirectoryPath);

        // Assert
        await Assert.That(actual.Cases.Single().RawFiles.Count).IsEqualTo(2);
        await Assert.That(actual.Cases.Single().BenchmarkReport!.Processes.Count).IsEqualTo(1);
    }
}
