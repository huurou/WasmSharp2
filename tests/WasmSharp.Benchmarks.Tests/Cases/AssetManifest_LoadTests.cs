using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class AssetManifest_LoadTests
{
    [Test]
    [Arguments("schema", "schema版2")]
    [Arguments("duplicate", "重複")]
    [Arguments("missing", "存在しない")]
    [Arguments("rooted", "相対path")]
    public async Task Manifestの版や素材参照が不正_理由を示して読み込みを拒否する(
        string scenario,
        string reason
    )
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var manifest = fixture.Manifest;
        fixture.SaveManifest(
            scenario switch
            {
                "schema" => manifest with { SchemaVersion = 2 },
                "duplicate" => manifest with { Assets = [manifest.Assets[0], manifest.Assets[0]] },
                "missing" => manifest with { Assets = [] },
                _ => manifest with
                {
                    Profile = manifest.Profile with
                    {
                        RelativePath = Path.Combine(
                            fixture.DirectoryPath,
                            manifest.Profile.RelativePath
                        ),
                    },
                },
            }
        );

        // Act & Assert
        var exception = await Assert
            .That(() => AssetManifest.Load(fixture.ManifestPath))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains(reason);
    }

    [Test]
    [Arguments("null", "JSONがnull")]
    [Arguments("{", "JSON形式が不正")]
    public async Task ManifestのJSONがnullまたは破損している_対象pathと理由を示して拒否する(
        string json,
        string reason
    )
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        File.WriteAllText(fixture.ManifestPath, json);

        // Act & Assert
        var exception = await Assert
            .That(() => AssetManifest.Load(fixture.ManifestPath))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains(fixture.ManifestPath).And.Contains(reason);
    }

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
    [Arguments("profile")]
    [Arguments("cases")]
    [Arguments("binary")]
    [Arguments("source")]
    public async Task 相対参照が同じ接頭辞の隣接ディレクトリを指す_manifest配下からの逸脱を拒否する(
        string reference
    )
    {
        // Arrange
        using var fixture = new AssetLoadingFixture();
        var original = reference switch
        {
            "cases" => fixture.Manifest.Cases,
            "binary" => fixture.Manifest.Assets[0].Binary,
            _ => fixture.Manifest.Profile,
        };
        var outsideDirectory = fixture.DirectoryPath + "-outside";
        Directory.CreateDirectory(outsideDirectory);
        var outsidePath = Path.Combine(outsideDirectory, Path.GetFileName(original.RelativePath));
        File.Copy(Path.Combine(fixture.DirectoryPath, original.RelativePath), outsidePath);
        var escaped = original with
        {
            RelativePath = Path.GetRelativePath(fixture.DirectoryPath, outsidePath),
        };
        var manifest = fixture.Manifest;
        fixture.SaveManifest(
            reference switch
            {
                "profile" => manifest with { Profile = escaped },
                "cases" => manifest with { Cases = escaped },
                "binary" => manifest with
                {
                    Assets = [manifest.Assets[0] with { Binary = escaped }],
                },
                _ => manifest with
                {
                    Assets =
                    [
                        manifest.Assets[0] with
                        {
                            Sources = [new(escaped.RelativePath, "fixture", escaped.Sha256)],
                        },
                    ],
                },
            }
        );

        // Act & Assert
        var exception = await Assert
            .That(() => AssetManifest.Load(fixture.ManifestPath))
            .Throws<InvalidDataException>();
        await Assert
            .That(exception!.Message)
            .Contains(escaped.RelativePath)
            .And.Contains("manifest配下");
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
