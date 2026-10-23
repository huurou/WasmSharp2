using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Jobs;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// runの実行前後に、比較条件と使用ファイルを記録する
/// </summary>
internal static class BenchmarkRun
{
    /// <summary>
    /// Jobの明示値とBDNへ委ねる既定値を区別してprocess構成を記録する
    /// </summary>
    internal static ProcessConfiguration DescribeProcess(string role, Job job)
    {
        return EnvironmentCapture.DescribeProcess(role, job);
    }

    /// <summary>
    /// 採用済み素材の内容を照合して、ケース定義・Job設定とともにrunへ保存する
    /// </summary>
    internal static void CaptureInputs(
        RunStore store,
        string assetManifestPath,
        IEnumerable<ProcessConfiguration> processes
    )
    {
        var loaded = AssetManifest.Load(assetManifestPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(assetManifestPath))!;
        var files = new[] { loaded.Manifest.Profile, loaded.Manifest.Cases }
            .Concat(loaded.Manifest.Assets.Select(x => x.Binary))
            .Concat(
                loaded
                    .Manifest.Assets.SelectMany(x => x.Sources)
                    .Select(x => new ManifestFile(x.RelativePath, x.Sha256))
            )
            .DistinctBy(x => x.RelativePath);
        ManifestFile? cases = null;
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, file.RelativePath));
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
            {
                throw new InvalidDataException(
                    "コピーする素材のSHA-256が一致しません: " + file.RelativePath
                );
            }

            var saved = store.SaveFile("inputs/assets/" + file.RelativePath, bytes);
            if (file.RelativePath == loaded.Manifest.Cases.RelativePath)
            {
                cases = saved;
            }
        }

        var manifest = store.SaveFile(
            "inputs/assets/asset-manifest.json",
            File.ReadAllBytes(assetManifestPath)
        );
        var job = store.SaveFile(
            "inputs/job.json",
            JsonSerializer.SerializeToUtf8Bytes(
                processes.Select(EnvironmentCapture.SnapshotProcess).ToList(),
                CaseJson.Options
            )
        );
        store.SaveInputs(manifest, cases!, job);
    }

    /// <summary>
    /// 採取processの環境と指定された測定・診断構成を同じrunへ保存する
    /// </summary>
    internal static RunEnvironment CaptureEnvironment(
        RunStore store,
        CommandInvocation command,
        IEnumerable<ProcessConfiguration> processes
    )
    {
        var environment = EnvironmentCapture.Capture(store, command, processes);
        store.SaveEnvironment(environment);
        return environment;
    }

    /// <summary>
    /// 使用source・assemblyと復元用の差分・未追跡ファイルを実行前に保存する
    /// </summary>
    internal static RunSource CaptureSourceBefore(
        string repositoryDirectory,
        RunStore store,
        IEnumerable<(AssemblyRole Role, string Path)> assemblies
    )
    {
        var source = new SourceCapture(repositoryDirectory).Before(store, assemblies);
        store.SaveSource(source);
        return source;
    }

    /// <summary>
    /// 実行前に使用したファイルと新しい使用sourceの内容変化を照合して追記する
    /// </summary>
    internal static RunSource CaptureSourceAfter(
        string repositoryDirectory,
        RunStore store,
        RunSource before
    )
    {
        var source = new SourceCapture(repositoryDirectory).After(before);
        store.SaveSource(source);
        return source;
    }
}
