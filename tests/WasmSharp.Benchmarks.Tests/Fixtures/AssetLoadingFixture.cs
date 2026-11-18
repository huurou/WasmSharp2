using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class AssetLoadingFixture : IDisposable
{
    private readonly CaseJson _json = new CaseJson();

    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "wasm-benchmark-assets-" + Guid.NewGuid().ToString("N")
    );

    internal string DirectoryPath => Path.Combine(_rootDirectory, "assets");
    internal string ManifestPath => Path.Combine(DirectoryPath, "asset-manifest.json");
    internal AssetManifest Manifest { get; private set; }
    internal List<BenchmarkCase> Definitions { get; }

    internal AssetLoadingFixture()
    {
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "wasm"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "Loading", "invoke.wasm"),
            Path.Combine(DirectoryPath, "wasm", "invoke.wasm")
        );
        File.WriteAllText(Path.Combine(DirectoryPath, "profile.json"), "{}");
        Definitions = [ReadCases(_json).Single(x => x.CaseId == "invoke.single")];
        File.WriteAllBytes(
            Path.Combine(DirectoryPath, "cases.json"),
            JsonSerializer.SerializeToUtf8Bytes(Definitions, _json.Options)
        );
        Manifest = new(
            1,
            FileDefinition("profile.json"),
            FileDefinition("cases.json"),
            [new("invoke", FileDefinition("wasm/invoke.wasm"), [], [])]
        );
        SaveManifest();
    }

    internal static List<BenchmarkCase> ReadCases(CaseJson json)
    {
        return JsonSerializer.Deserialize<List<BenchmarkCase>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")),
            json.Options
        )!;
    }

    internal static BenchmarkCase WithInput(BenchmarkCase definition, CaseInput input, CaseJson json)
    {
        return definition with
        {
            Input = input,
            InputHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, json.Options))),
        };
    }

    internal void SaveCases()
    {
        File.WriteAllBytes(
            Path.Combine(DirectoryPath, "cases.json"),
            JsonSerializer.SerializeToUtf8Bytes(Definitions, _json.Options)
        );
        Manifest = Manifest with { Cases = FileDefinition("cases.json") };
        SaveManifest();
    }

    internal void SaveAssets(LoadedAssets assets)
    {
        var binaries = assets.Catalog.Cases.Select(x => x.AssetId).Distinct().ToDictionary(x => x, assets.GetBinary);
        Definitions.Clear();
        foreach (var definition in assets.Catalog.Cases)
        {
            Definitions.Add(
                definition with
                {
                    AssetSha256 = Convert.ToHexStringLower(SHA256.HashData(binaries[definition.AssetId].AsSpan())),
                }
            );
        }
        foreach (var binary in binaries)
        {
            File.WriteAllBytes(Path.Combine(DirectoryPath, "wasm", binary.Key + ".wasm"), binary.Value.AsSpan());
        }
        SaveCases();
        SaveManifest(
            Manifest with
            {
                Assets =
                [
                    .. binaries.Keys.Select(x => new AssetDefinition(x, FileDefinition("wasm/" + x + ".wasm"), [], [])),
                ],
            }
        );
    }

    internal void SaveManifest()
    {
        File.WriteAllBytes(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(Manifest, _json.Options));
    }

    internal void SaveManifest(AssetManifest manifest)
    {
        Manifest = manifest;
        SaveManifest();
    }

    private ManifestFile FileDefinition(string path)
    {
        return new(path, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(DirectoryPath, path)))));
    }

    public void Dispose()
    {
        Directory.Delete(_rootDirectory, recursive: true);
    }
}
