using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class CaseCatalog_GetTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();

    [Test]
    public async Task 存在しないケースIDを選ぶ_選択IDを示して拒否する()
    {
        // Arrange
        var catalog = new CaseCatalog(AssetLoadingFixture.ReadCases(json_), json_, methods_);

        // Act & Assert
        var exception = await Assert
            .That(() => catalog.Get("unknown.case"))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("unknown.case");
    }
}
