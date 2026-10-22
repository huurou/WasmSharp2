using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class CaseCatalog_GetTests
{
    [Test]
    public async Task 存在しないケースIDを選ぶ_選択IDを示して拒否する()
    {
        // Arrange
        var catalog = new CaseCatalog(AssetLoadingFixture.ReadCases());

        // Act & Assert
        var exception = await Assert
            .That(() => catalog.Get("unknown.case"))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("unknown.case");
    }
}
