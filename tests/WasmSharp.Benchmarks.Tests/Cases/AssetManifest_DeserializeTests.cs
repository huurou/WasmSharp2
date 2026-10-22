using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class AssetManifest_DeserializeTests
{
    [Test]
    public async Task 小さい素材fixtureを読む_相対pathと生成条件のhashを復元する()
    {
        // Arrange
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "asset-manifest.json")
        );

        // Act
        var manifest = JsonSerializer.Deserialize<AssetManifest>(json, CaseJson.Options)!;
        var restored = JsonSerializer.Deserialize<AssetManifest>(
            JsonSerializer.Serialize(manifest, CaseJson.Options),
            CaseJson.Options
        )!;
        var asset = restored.Assets.Single();
        var step = asset.GenerationSteps.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(restored.SchemaVersion).IsEqualTo(1U);
            await Assert
                .That(restored.Profile)
                .IsEqualTo(new ManifestFile("core2-profile.json", new string('1', 64)));
            await Assert
                .That(restored.Cases)
                .IsEqualTo(new ManifestFile("cases.json", new string('2', 64)));
            await Assert.That(asset.AssetId).IsEqualTo("fixture");
            await Assert
                .That(asset.Binary)
                .IsEqualTo(new ManifestFile("wasm/fixture.wasm", new string('a', 64)));
            await Assert.That(Path.IsPathFullyQualified(asset.Binary.RelativePath)).IsFalse();
            await Assert
                .That(asset.Sources.Single())
                .IsEqualTo(
                    new AssetSource("sources/fixture.wat", "fixture-revision", new string('3', 64))
                );
            await Assert.That(step.Tool.Name).IsEqualTo("wat2wasm");
            await Assert.That(step.Tool.Version).IsEqualTo("1.0.42");
            await Assert.That(step.Tool.Revision).IsEqualTo("fixture-tool-revision");
            await Assert.That(step.Tool.BuildConfiguration).IsEqualTo("Release");
            await Assert.That(step.Tool.Executable.Sha256).IsEqualTo(new string('4', 64));
            await Assert
                .That(
                    step.Arguments.SequenceEqual(
                        new[] { "sources/fixture.wat", "-o", "wasm/fixture.wasm" }
                    )
                )
                .IsTrue();
            await Assert.That(step.ArgumentsSha256).IsEqualTo(new string('5', 64));
        }
    }
}
