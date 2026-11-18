using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 新規run内に条件と観測を保存し、最後に完了を確定する
/// </summary>
internal sealed class RunStore
{
    private readonly string _outputDirectory;
    private readonly CaseJson _json;
    private RunManifest _manifest;
    private bool _savingFailed;

    private RunStore(string outputDirectory, RunManifest manifest, CaseJson json)
    {
        _outputDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        _json = json;
        _manifest = manifest;
    }

    /// <summary>
    /// 子processが未完了runへ検査snapshotを追記するために開く
    /// </summary>
    internal static RunStore OpenChecks(string outputDirectory, string runId, CaseJson json)
    {
        var manifest = JsonSerializer.Deserialize<RunManifest>(
            File.ReadAllBytes(Path.Combine(outputDirectory, "run.json")),
            json.Options
        )!;
        if (manifest.RunId != runId || manifest.CompletedAtUtc is not null)
        {
            throw new InvalidDataException("検査の保存先runが一致しないか完了しています。");
        }
        return new RunStore(outputDirectory, manifest, json);
    }

    /// <summary>
    /// 追記した検査snapshotを保存参照とともに読み出す
    /// </summary>
    internal static List<(ManifestFile File, ProcessCheck Check)> ReadChecks(string outputDirectory, CaseJson json)
    {
        var directory = Path.Combine(outputDirectory, "checks");
        if (!Directory.Exists(directory))
        {
            return [];
        }
        return
        [
            .. Directory
                .GetFiles(directory, "*.json")
                .Select(x =>
                {
                    var bytes = File.ReadAllBytes(x);
                    return (
                        new ManifestFile(
                            Path.GetRelativePath(outputDirectory, x).Replace('\\', '/'),
                            Convert.ToHexStringLower(SHA256.HashData(bytes))
                        ),
                        JsonSerializer.Deserialize<ProcessCheck>(bytes, json.Options)!
                    );
                }),
        ];
    }

    /// <summary>
    /// 予定ケースと識別情報を持つ未完了runを新規作成する
    /// </summary>
    internal static RunStore Create(string outputDirectory, RunManifest manifest, CaseJson json)
    {
        if (Path.Exists(outputDirectory))
        {
            throw new IOException("出力先は既に存在します。");
        }

        Directory.CreateDirectory(outputDirectory);
        var store = new RunStore(
            Path.GetFullPath(outputDirectory),
            manifest with
            {
                CompletedAtUtc = null,
                Status = ResultStatus.Incomplete,
                PlannedCaseIds = [.. manifest.PlannedCaseIds],
                Cases = [],
                EnvironmentFile = null,
                SourceFile = null,
                AssetManifestFile = null,
                CasesFile = null,
                JobSettingsFile = null,
                Failures = [],
            },
            json
        );
        store.SaveManifest(store._manifest, overwrite: false);
        return store;
    }

    /// <summary>
    /// 完了記録と保存参照を照合し、未確定または不整合なrunを未完了で返す
    /// </summary>
    internal static RunManifest Load(string outputDirectory, CaseJson json)
    {
        var manifest = JsonSerializer.Deserialize<RunManifest>(
            File.ReadAllBytes(Path.Combine(outputDirectory, "run.json")),
            json.Options
        )!;
        if (manifest.CompletedAtUtc is null)
        {
            return AsIncomplete(manifest, "完了記録がありません。");
        }

        try
        {
            if (manifest.Status == ResultStatus.Incomplete || !HasCompletedCases(manifest))
            {
                return AsIncomplete(manifest, "予定ケースが完了していません。");
            }

            new RunStore(Path.GetFullPath(outputDirectory), manifest, json).VerifyReferences();
            return manifest;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or JsonException
                        or ArgumentException
                        or InvalidOperationException
                        or UnauthorizedAccessException
            )
        {
            return AsIncomplete(manifest, "保存参照の照合に失敗しました: " + exception.Message);
        }
    }

    /// <summary>
    /// 素材・設定・source・rawの内容をrun内へ新規保存し、相対参照とhashを返す
    /// </summary>
    internal ManifestFile SaveFile(string relativePath, ReadOnlySpan<byte> content)
    {
        EnsureOpen();
        try
        {
            var path = GetPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            stream.Write(content);
            return new(
                Path.GetRelativePath(_outputDirectory, path).Replace('\\', '/'),
                Convert.ToHexStringLower(SHA256.HashData(content))
            );
        }
        catch
        {
            _savingFailed = true;
            throw;
        }
    }

    /// <summary>
    /// run内に保存した素材一覧・ケース定義・Job設定のコピーを対応付ける
    /// </summary>
    internal void SaveInputs(ManifestFile assets, ManifestFile cases, ManifestFile job)
    {
        SaveManifest(_manifest with { AssetManifestFile = assets, CasesFile = cases, JobSettingsFile = job });
    }

    /// <summary>
    /// 収集済みの環境を保存してrunへ対応付ける
    /// </summary>
    internal void SaveEnvironment(RunEnvironment environment)
    {
        var file = SaveFile("environment.json", JsonSerializer.SerializeToUtf8Bytes(environment, _json.Options));
        SaveManifest(_manifest with { EnvironmentFile = file });
    }

    /// <summary>
    /// 収集済みのsource情報を追記し、最新の記録をrunへ対応付ける
    /// 保存参照はrun基準とし、未追跡コピーはリポジトリと同じ相対構造へ配置する
    /// </summary>
    internal void SaveSource(RunSource source)
    {
        var file = SaveFile(
            "source/source-" + Guid.NewGuid().ToString() + ".json",
            JsonSerializer.SerializeToUtf8Bytes(source, _json.Options)
        );
        SaveManifest(_manifest with { SourceFile = file });
    }

    /// <summary>
    /// 同じrun・ケースのprocess検査を段階ごとに別ファイルへ追記する
    /// </summary>
    internal ManifestFile SaveCheck(ProcessCheck check)
    {
        if (check.RunId != _manifest.RunId || !_manifest.PlannedCaseIds.Contains(check.CaseId))
        {
            throw new ArgumentException("検査のrunまたはケースが保存先と一致しません。", nameof(check));
        }

        return SaveFile(
            "checks/" + Guid.NewGuid().ToString() + ".json",
            JsonSerializer.SerializeToUtf8Bytes(check, _json.Options)
        );
    }

    /// <summary>
    /// ケースごとの最終結果とraw・process検査の参照を追記する
    /// </summary>
    internal void SaveCase(CaseRunResult result)
    {
        SaveManifest(_manifest with { Cases = [.. _manifest.Cases, result] });
    }

    /// <summary>
    /// runが未完了のまま終了するときも観測した原因を保持する
    /// </summary>
    internal void SaveFailure(RunFailure failure)
    {
        SaveManifest(_manifest with { Failures = [.. _manifest.Failures, failure] });
    }

    /// <summary>
    /// 全予定ケースと保存参照を照合した後に、呼出元が判定した状態で完了を確定する
    /// </summary>
    internal void Complete(ResultStatus status, List<RunFailure>? failures = null)
    {
        EnsureOpen();
        if (_savingFailed)
        {
            throw new InvalidOperationException("保存に失敗したrunは完了できません。");
        }

        if (status == ResultStatus.Incomplete || !HasCompletedCases(_manifest))
        {
            throw new InvalidOperationException("予定ケースが完了していません。");
        }

        try
        {
            VerifyReferences();
            SaveManifest(
                _manifest with
                {
                    Status = status,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Failures = failures ?? [],
                }
            );
        }
        catch
        {
            _savingFailed = true;
            throw;
        }
    }

    private static bool HasCompletedCases(RunManifest manifest)
    {
        return manifest.Cases.Count == manifest.PlannedCaseIds.Count
            && manifest.Cases.All(x => x.Status != ResultStatus.Incomplete)
            && manifest.Cases.Select(x => x.CaseId).ToHashSet().SetEquals(manifest.PlannedCaseIds);
    }

    private static RunManifest AsIncomplete(RunManifest manifest, string reason)
    {
        return manifest with
        {
            Status = ResultStatus.Incomplete,
            CompletedAtUtc = null,
            Failures =
            [
                .. manifest.Failures,
                new(null, RunStage.Saving, null, "runの完了が確定していません。", null, null, reason),
            ],
        };
    }

    private void VerifyReferences()
    {
        foreach (
            var file in new[]
            {
                _manifest.EnvironmentFile,
                _manifest.AssetManifestFile,
                _manifest.CasesFile,
                _manifest.JobSettingsFile,
            }
        )
        {
            ReadSavedFile(file);
        }

        var source =
            JsonSerializer.Deserialize<RunSource>(ReadSavedFile(_manifest.SourceFile), _json.Options)
            ?? throw new InvalidDataException("source情報が保存されていません。");
        ReadSavedFile(source.IndexDiff);
        ReadSavedFile(source.WorktreeDiff);
        foreach (var file in source.UntrackedFiles)
        {
            ReadSavedFile(file);
        }

        foreach (var result in _manifest.Cases)
        {
            foreach (var file in result.RawFiles)
            {
                ReadSavedFile(file);
            }

            if (result.Metrics.Time is { } time)
            {
                ReadSavedFile(time.RawMeasurements);
            }

            foreach (var process in result.BenchmarkReport?.Processes ?? [])
            {
                var file = process.CheckFile;
                if (file is null)
                {
                    continue;
                }

                var check = JsonSerializer.Deserialize<ProcessCheck>(ReadSavedFile(file), _json.Options)!;
                if (
                    check.RunId != _manifest.RunId
                    || check.CaseId != result.CaseId
                    || check.JobId != process.JobId
                    || check.ProcessId != process.ProcessId
                )
                {
                    throw new InvalidDataException("ケースとprocess検査の対応が一致しません。");
                }
            }
        }
    }

    private byte[] ReadSavedFile(ManifestFile? file)
    {
        if (file is null)
        {
            throw new InvalidOperationException("runの条件が保存されていません。");
        }

        var content = File.ReadAllBytes(GetPath(file.RelativePath));
        if (Convert.ToHexStringLower(SHA256.HashData(content)) != file.Sha256)
        {
            throw new IOException("保存ファイルのSHA-256が一致しません: " + file.RelativePath);
        }

        return content;
    }

    private void EnsureOpen()
    {
        if (_manifest.CompletedAtUtc is not null)
        {
            throw new InvalidOperationException("完了したrunは変更できません。");
        }
    }

    private string GetPath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_outputDirectory, relativePath));
        if (
            Path.IsPathRooted(relativePath)
            || !path.StartsWith(_outputDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException("保存先はrun内の相対pathで指定してください。", nameof(relativePath));
        }

        return path;
    }

    private void SaveManifest(RunManifest manifest, bool overwrite = true)
    {
        EnsureOpen();
        try
        {
            var temporaryPath = Path.Combine(_outputDirectory, Guid.NewGuid().ToString());
            var content = JsonSerializer.SerializeToUtf8Bytes(manifest, _json.Options);
            // 呼出元がDTOの一覧を変更しても、保存済みの対応を変えない
            var snapshot = JsonSerializer.Deserialize<RunManifest>(content, _json.Options)!;
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, Path.Combine(_outputDirectory, "run.json"), overwrite);
            _manifest = snapshot;
        }
        catch
        {
            _savingFailed = true;
            throw;
        }
    }
}
