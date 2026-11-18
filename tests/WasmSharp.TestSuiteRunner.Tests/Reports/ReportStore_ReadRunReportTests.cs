using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_ReadRunReportTests
{
    private readonly ReportStore _reportStore = new ReportStore();

    [Test]
    public async Task Manifestを指定する_実行結果として受け付けない()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = await directory.WriteJsonAsync("manifest.json", CorpusManifestFixture.Create());

        // Act & Assert
        var exception = await Assert.That(() => _reportStore.ReadRunReport(path)).ThrowsExactly<ReportStoreException>();
        await Assert.That(exception!.Message).Contains("run_report");
    }
}
