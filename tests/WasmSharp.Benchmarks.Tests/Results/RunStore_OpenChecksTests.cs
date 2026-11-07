using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_OpenChecksTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 別runまたは完了したrunである_検査追記用に開かない(bool completed)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        if (completed)
        {
            fixture.SaveConditions(store);
            fixture.SaveSucceededCase(store);
            store.Complete(ResultStatus.Succeeded);
        }
        var expected = File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, "run.json"));

        // Act & Assert
        var exception = await Assert
            .That(() =>
                RunStore.OpenChecks(
                    fixture.DirectoryPath,
                    completed ? fixture.Manifest.RunId : "other-run"
                )
            )
            .ThrowsExactly<InvalidDataException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains("一致しないか完了");
            await Assert
                .That(File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, "run.json")))
                .IsEquivalentTo(expected);
        }
    }
}
