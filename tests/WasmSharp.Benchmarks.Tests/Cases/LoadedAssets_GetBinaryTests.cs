using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class LoadedAssets_GetBinaryTests
{
    [Test]
    public async Task 未登録の素材を指定する_識別子と理由を示して拒否する()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var assets = AssetManifest.Load(fixture.ManifestPath);

        // Act & Assert
        var exception = await Assert
            .That(() => assets.GetBinary("unknown-asset"))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("unknown-asset").And.Contains("未登録");
    }
}
