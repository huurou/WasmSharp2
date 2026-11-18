using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_ReadManifestTests
{
    private readonly ReportStore _reportStore = new ReportStore();

    [Test]
    public async Task 実行結果を指定する_manifestとして受け付けない()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = await directory.WriteJsonAsync("run.json", RunReportFixture.CreateSample());

        // Act & Assert
        var exception = await Assert.That(() => _reportStore.ReadManifest(path)).ThrowsExactly<ReportStoreException>();
        await Assert.That(exception!.Message).Contains("corpus_manifest");
    }
}
