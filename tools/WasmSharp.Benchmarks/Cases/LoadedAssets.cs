using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 保存用manifestから読み込んだ素材と入力を保持し、計時時のI/Oと入力生成を不要にする。
/// </summary>
internal sealed class LoadedAssets(
    AssetManifest manifest,
    CaseCatalog catalog,
    ImmutableDictionary<string, ImmutableArray<byte>> binaries,
    ImmutableDictionary<string, PreparedInput> inputs
)
{
    internal AssetManifest Manifest { get; } = manifest;
    internal CaseCatalog Catalog { get; } = catalog;

    internal ImmutableArray<byte> GetBinary(string assetId) =>
        binaries.TryGetValue(assetId, out var bytes)
            ? bytes
            : throw new InvalidDataException($"素材'{assetId}'は未登録です。");

    internal PreparedInput GetInput(string caseId) =>
        inputs.TryGetValue(caseId, out var input)
            ? input
            : throw new InvalidDataException($"ケース'{caseId}'は未登録です。");

    internal static LoadedAssets Load(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var manifest = ReadJson<AssetManifest>(File.ReadAllBytes(path), path);
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"manifestのschema版{manifest.SchemaVersion}には対応していません。"
            );
        }

        ReadVerified(directory, manifest.Profile);
        var catalog = new CaseCatalog(
            ReadJson<List<BenchmarkCase>>(
                ReadVerified(directory, manifest.Cases),
                manifest.Cases.RelativePath
            )
        );
        var binaries = ImmutableDictionary.CreateBuilder<string, ImmutableArray<byte>>(
            StringComparer.Ordinal
        );
        foreach (var asset in manifest.Assets)
        {
            if (
                !binaries.TryAdd(
                    asset.AssetId,
                    ReadVerified(directory, asset.Binary).ToImmutableArray()
                )
            )
            {
                throw new InvalidDataException($"素材'{asset.AssetId}'が重複しています。");
            }
        }

        var inputs = ImmutableDictionary.CreateBuilder<string, PreparedInput>(
            StringComparer.Ordinal
        );
        foreach (var definition in catalog.Cases)
        {
            var asset = manifest.Assets.SingleOrDefault(x => x.AssetId == definition.AssetId);
            if (asset is null || asset.Binary.Sha256 != definition.AssetSha256)
            {
                throw new InvalidDataException(
                    $"ケース'{definition.CaseId}'の素材'{definition.AssetId}'が存在しないか、素材hashが一致しません。"
                );
            }
            inputs.Add(definition.CaseId, InputFactory.Create(definition));
        }

        return new LoadedAssets(manifest, catalog, binaries.ToImmutable(), inputs.ToImmutable());
    }

    private static byte[] ReadVerified(string directory, ManifestFile file)
    {
        if (Path.IsPathRooted(file.RelativePath))
        {
            throw new InvalidDataException(
                $"素材path'{file.RelativePath}'はmanifestからの相対pathで指定してください。"
            );
        }
        var bytes = File.ReadAllBytes(Path.Combine(directory, file.RelativePath));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
        {
            throw new InvalidDataException(
                $"素材path'{file.RelativePath}'のSHA-256が一致しません。"
            );
        }
        return bytes;
    }

    private static T ReadJson<T>(byte[] bytes, string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, CaseJson.Options)
                ?? throw new InvalidDataException($"'{path}'のJSONがnullです。");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"'{path}'のJSON形式が不正です: {exception.Message}",
                exception
            );
        }
    }
}
