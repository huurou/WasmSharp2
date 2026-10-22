using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_CreateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 出力先が既に存在する_既存内容を変えず作成を拒否する(bool directory)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        if (directory)
        {
            Directory.CreateDirectory(fixture.DirectoryPath);
        }
        var existingPath = directory
            ? Path.Combine(fixture.DirectoryPath, "existing.txt")
            : fixture.DirectoryPath;
        File.WriteAllText(existingPath, "採用済み性能基準値");

        try
        {
            // Act & Assert
            await Assert
                .That(() => RunStore.Create(fixture.DirectoryPath, fixture.Manifest))
                .ThrowsExactly<IOException>();
            await Assert.That(File.ReadAllText(existingPath)).IsEqualTo("採用済み性能基準値");
        }
        finally
        {
            if (!directory)
            {
                File.Delete(existingPath);
            }
        }
    }

    [Test]
    public async Task 新規runを作成する_予定ケースと未完了状態を先に保存する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();

        // Act
        RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        var actual = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.RunId).IsEqualTo("fixture-success");
            await Assert.That(actual.PlannedCaseIds).IsEquivalentTo(["invoke.single"]);
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(actual.CompletedAtUtc).IsNull();
            await Assert.That(actual.Cases).IsEmpty();
        }
    }
}
