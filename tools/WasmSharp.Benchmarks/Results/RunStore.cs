using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 新規run内に条件と観測を保存し、最後に完了を確定する
/// </summary>
internal sealed class RunStore
{
    private readonly string outputDirectory_;
    private RunManifest manifest_;
    private bool savingFailed_;

    private RunStore(string outputDirectory, RunManifest manifest)
    {
        outputDirectory_ = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        manifest_ = manifest;
    }

    /// <summary>
    /// 予定ケースと識別情報を持つ未完了runを新規作成する
    /// </summary>
    internal static RunStore Create(string outputDirectory, RunManifest manifest)
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
                PlannedCaseIds = manifest.PlannedCaseIds.ToList(),
                Cases = [],
                EnvironmentFile = null,
                SourceFile = null,
                AssetManifestFile = null,
                CasesFile = null,
                JobSettingsFile = null,
                Failures = [],
            }
        );
        store.SaveManifest(store.manifest_, overwrite: false);
        return store;
    }

    /// <summary>
    /// 完了記録と保存参照を照合し、未確定または不整合なrunを未完了で返す
    /// </summary>
    internal static RunManifest Load(string outputDirectory)
    {
        var manifest = JsonSerializer.Deserialize<RunManifest>(
            File.ReadAllBytes(Path.Combine(outputDirectory, "run.json")),
            CaseJson.Options
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

            new RunStore(Path.GetFullPath(outputDirectory), manifest).VerifyReferences();
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
                Path.GetRelativePath(outputDirectory_, path).Replace('\\', '/'),
                Convert.ToHexStringLower(SHA256.HashData(content))
            );
        }
        catch
        {
            savingFailed_ = true;
            throw;
        }
    }

    /// <summary>
    /// run内に保存した素材一覧・ケース定義・Job設定のコピーを対応付ける
    /// </summary>
    internal void SaveInputs(ManifestFile assets, ManifestFile cases, ManifestFile job) =>
        SaveManifest(
            manifest_ with
            {
                AssetManifestFile = assets,
                CasesFile = cases,
                JobSettingsFile = job,
            }
        );

    /// <summary>
    /// 収集済みの環境を保存してrunへ対応付ける
    /// </summary>
    internal void SaveEnvironment(RunEnvironment environment)
    {
        var file = SaveFile(
            "environment.json",
            JsonSerializer.SerializeToUtf8Bytes(environment, CaseJson.Options)
        );
        SaveManifest(manifest_ with { EnvironmentFile = file });
    }

    /// <summary>
    /// 収集済みのsource情報を追記し、最新の記録をrunへ対応付ける
    /// 保存参照はrun基準とし、未追跡コピーはリポジトリと同じ相対構造へ配置する
    /// </summary>
    internal void SaveSource(RunSource source)
    {
        var file = SaveFile(
            "source/source-" + Guid.NewGuid().ToString("N") + ".json",
            JsonSerializer.SerializeToUtf8Bytes(source, CaseJson.Options)
        );
        SaveManifest(manifest_ with { SourceFile = file });
    }

    /// <summary>
    /// 同じrun・ケースのprocess検査を段階ごとに別ファイルへ追記する
    /// </summary>
    internal ManifestFile SaveCheck(ProcessCheck check)
    {
        if (check.RunId != manifest_.RunId || !manifest_.PlannedCaseIds.Contains(check.CaseId))
        {
            throw new ArgumentException(
                "検査のrunまたはケースが保存先と一致しません。",
                nameof(check)
            );
        }

        return SaveFile(
            "checks/" + Guid.NewGuid().ToString("N") + ".json",
            JsonSerializer.SerializeToUtf8Bytes(check, CaseJson.Options)
        );
    }

    /// <summary>
    /// ケースごとの最終結果とraw・process検査の参照を追記する
    /// </summary>
    internal void SaveCase(CaseRunResult result) =>
        SaveManifest(manifest_ with { Cases = [.. manifest_.Cases, result] });

    /// <summary>
    /// 全予定ケースと保存参照を照合した後に、呼出元が判定した状態で完了を確定する
    /// </summary>
    internal void Complete(ResultStatus status, List<RunFailure>? failures = null)
    {
        EnsureOpen();
        if (savingFailed_)
        {
            throw new InvalidOperationException("保存に失敗したrunは完了できません。");
        }

        if (status == ResultStatus.Incomplete || !HasCompletedCases(manifest_))
        {
            throw new InvalidOperationException("予定ケースが完了していません。");
        }

        try
        {
            VerifyReferences();
            SaveManifest(
                manifest_ with
                {
                    Status = status,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Failures = failures ?? [],
                }
            );
        }
        catch
        {
            savingFailed_ = true;
            throw;
        }
    }

    private static bool HasCompletedCases(RunManifest manifest) =>
        manifest.Cases.Count == manifest.PlannedCaseIds.Count
        && manifest.Cases.All(x => x.Status != ResultStatus.Incomplete)
        && manifest.Cases.Select(x => x.CaseId).ToHashSet().SetEquals(manifest.PlannedCaseIds);

    private static RunManifest AsIncomplete(RunManifest manifest, string reason) =>
        manifest with
        {
            Status = ResultStatus.Incomplete,
            CompletedAtUtc = null,
            Failures =
            [
                .. manifest.Failures,
                new(
                    null,
                    RunStage.Saving,
                    null,
                    "runの完了が確定していません。",
                    null,
                    null,
                    reason
                ),
            ],
        };

    private void VerifyReferences()
    {
        foreach (
            var file in new[]
            {
                manifest_.EnvironmentFile,
                manifest_.AssetManifestFile,
                manifest_.CasesFile,
                manifest_.JobSettingsFile,
            }
        )
        {
            ReadSavedFile(file);
        }

        var source =
            JsonSerializer.Deserialize<RunSource>(
                ReadSavedFile(manifest_.SourceFile),
                CaseJson.Options
            ) ?? throw new InvalidDataException("source情報が保存されていません。");
        ReadSavedFile(source.IndexDiff);
        ReadSavedFile(source.WorktreeDiff);
        foreach (var file in source.UntrackedFiles)
        {
            ReadSavedFile(file);
        }

        foreach (var result in manifest_.Cases)
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
                if (process.CheckFile is not { } file)
                {
                    continue;
                }

                var check = JsonSerializer.Deserialize<ProcessCheck>(
                    ReadSavedFile(file),
                    CaseJson.Options
                )!;
                if (
                    check.RunId != manifest_.RunId
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
        if (manifest_.CompletedAtUtc is not null)
        {
            throw new InvalidOperationException("完了したrunは変更できません。");
        }
    }

    private string GetPath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(outputDirectory_, relativePath));
        if (
            Path.IsPathRooted(relativePath)
            || !path.StartsWith(
                outputDirectory_ + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new ArgumentException(
                "保存先はrun内の相対pathで指定してください。",
                nameof(relativePath)
            );
        }

        return path;
    }

    private void SaveManifest(RunManifest manifest, bool overwrite = true)
    {
        EnsureOpen();
        try
        {
            var temporaryPath = Path.Combine(
                outputDirectory_,
                Guid.NewGuid().ToString("N") + ".tmp"
            );
            var content = JsonSerializer.SerializeToUtf8Bytes(manifest, CaseJson.Options);
            // 呼出元がDTOの一覧を変更しても、保存済みの対応を変えない
            var snapshot = JsonSerializer.Deserialize<RunManifest>(content, CaseJson.Options)!;
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, Path.Combine(outputDirectory_, "run.json"), overwrite);
            manifest_ = snapshot;
        }
        catch
        {
            savingFailed_ = true;
            throw;
        }
    }
}
