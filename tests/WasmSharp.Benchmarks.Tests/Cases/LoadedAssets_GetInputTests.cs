using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class LoadedAssets_GetInputTests
{
    private readonly CaseJson _json = new CaseJson();
    private readonly MeasurementMethods _methods = new MeasurementMethods();

    [Test]
    public async Task 未登録のケースを指定する_識別子と理由を示して拒否する()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var assets = AssetManifest.Load(fixture.ManifestPath, _json, _methods);

        // Act & Assert
        var exception = await Assert.That(() => assets.GetInput("unknown-case")).ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("unknown-case").And.Contains("未登録");
    }
}
