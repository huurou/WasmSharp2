using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Fixtures;

internal sealed class AssetLoadingFixture : IDisposable
{
    internal string DirectoryPath { get; } =
        Path.Combine(Path.GetTempPath(), "wasm-benchmark-assets-" + Guid.NewGuid().ToString("N"));
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
        Definitions = [ReadCases().Single(x => x.CaseId == "invoke.single")];
        File.WriteAllBytes(
            Path.Combine(DirectoryPath, "cases.json"),
            JsonSerializer.SerializeToUtf8Bytes(Definitions, CaseJson.Options)
        );
        Manifest = new(
            1,
            FileDefinition("profile.json"),
            FileDefinition("cases.json"),
            [new("invoke", FileDefinition("wasm/invoke.wasm"), [], [])]
        );
        SaveManifest();
    }

    internal static List<BenchmarkCase> ReadCases() =>
        JsonSerializer.Deserialize<List<BenchmarkCase>>(
            File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "cases.json")
            ),
            CaseJson.Options
        )!;

    internal static BenchmarkCase WithInput(BenchmarkCase definition, CaseInput input) =>
        definition with
        {
            Input = input,
            InputHash = Convert.ToHexStringLower(
                SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, CaseJson.Options))
            ),
        };

    internal void SaveCases()
    {
        File.WriteAllBytes(
            Path.Combine(DirectoryPath, "cases.json"),
            JsonSerializer.SerializeToUtf8Bytes(Definitions, CaseJson.Options)
        );
        Manifest = Manifest with { Cases = FileDefinition("cases.json") };
        SaveManifest();
    }

    internal void SaveManifest() =>
        File.WriteAllBytes(
            ManifestPath,
            JsonSerializer.SerializeToUtf8Bytes(Manifest, CaseJson.Options)
        );

    private ManifestFile FileDefinition(string path) =>
        new(
            path,
            Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(Path.Combine(DirectoryPath, path)))
            )
        );

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}
