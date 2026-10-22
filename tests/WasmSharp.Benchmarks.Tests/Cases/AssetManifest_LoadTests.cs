using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class AssetManifest_LoadTests
{
    [Test]
    public async Task 正常な素材fixtureを読む_相対参照と読込済みバイト列から公開Invokeを準備できる()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();

        // Act
        var loaded = AssetManifest.Load(fixture.ManifestPath);
        File.Delete(Path.Combine(fixture.DirectoryPath, "wasm", "invoke.wasm"));
        var definition = loaded.Catalog.Get("invoke.single");
        var input = loaded.GetInput(definition.CaseId);
        var instance = WasmModule
            .Decode(loaded.GetBinary(definition.AssetId).AsSpan())
            .Validate()
            .Instantiate(new WasmImports());
        var results = instance.GetFunction(input.FunctionExport).Invoke(input.Arguments.AsSpan());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded.Manifest.SchemaVersion).IsEqualTo(1U);
            await Assert.That(definition.InputHash).IsEqualTo(fixture.Definitions[0].InputHash);
            await Assert.That(results.Values[0].AsI32()).IsEqualTo(-42);
        }
    }

    [Test]
    [Arguments("profile.json")]
    [Arguments("cases.json")]
    [Arguments("wasm/invoke.wasm")]
    public async Task 参照先の内容が変更された_対象pathとhash不一致の理由を返す(string relativePath)
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        File.AppendAllText(Path.Combine(fixture.DirectoryPath, relativePath), "changed");

        // Act & Assert
        var exception = await Assert
            .That(() => AssetManifest.Load(fixture.ManifestPath))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains(relativePath).And.Contains("SHA-256");
    }

    [Test]
    public async Task ケースの素材hashが異なる_ケースIDと理由を返す()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        fixture.Definitions[0] = fixture.Definitions[0] with { AssetSha256 = new string('0', 64) };
        fixture.SaveCases();

        // Act & Assert
        var exception = await Assert
            .That(() => AssetManifest.Load(fixture.ManifestPath))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("invoke.single").And.Contains("素材");
    }

    [Test]
    public async Task 入力だけが変更された_入力hash不一致を測定前に拒否する()
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        fixture.Definitions[0].Input.Arguments[0] = new(NumericType.I32, "00000001");
        fixture.SaveCases();

        // Act & Assert
        var exception = await Assert
            .That(() => AssetManifest.Load(fixture.ManifestPath))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("invoke.single").And.Contains("入力hash");
    }
}
