using System.Collections.Immutable;
using System.Security.Cryptography;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 生成時と実行時に、manifestと素材の相対path・hash・参照・所有を照合する
/// </summary>
/// <remarks>
/// 公式入力やbaselineを素材として巻き込まないための境界の検査であり、Wasmの意味論は検査しない。
/// </remarks>
internal static class CorpusVerifier
{
    private const string OPERATION = "verify";
    private const string MATERIAL_ROOT = "modules";

    /// <summary>
    /// manifestに記録した素材を、manifestの親を基準にした相対配置で照合する。
    /// </summary>
    /// <param name="manifest">照合するmanifest</param>
    /// <param name="manifestPath">manifestの配置先。親ディレクトリを素材の基準にする</param>
    /// <param name="mode">元入力も照合するかどうかを決める工程</param>
    /// <param name="sourceRoot">生成工程で照合する公式入力のspec-root。実行工程では使用しない</param>
    internal static CorpusVerification Verify(
        CorpusManifest manifest,
        string manifestPath,
        VerificationMode mode,
        string? sourceRoot
    )
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var inputRoot =
            mode == VerificationMode.Generation
                ? Path.Combine(
                    sourceRoot ?? throw new ArgumentNullException(nameof(sourceRoot)),
                    manifest.Profile.WorkingDirectory
                )
                : null;
        var artifacts = manifest
            .Inputs.SelectMany(x => x.Artifacts.Select(y => (Artifact: y, Owner: x.Input.Path)))
            .ToArray();
        // 入力をまたぐ重複と、他の入力が所有する素材への参照を検出するため、全記録を先に集める。
        var recordCounts = artifacts
            .CountBy(x => x.Artifact.Path, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
        var owners = artifacts
            .DistinctBy(x => x.Artifact.Path, StringComparer.Ordinal)
            .ToDictionary(x => x.Artifact.Path, x => x.Owner, StringComparer.Ordinal);
        var inputs = manifest
            .Inputs.Select(x => VerifyInput(x, root, mode, inputRoot, recordCounts, owners))
            .ToImmutableArray();
        IEnumerable<CorpusDiagnostic> diagnostics = FindExtraMaterials(root, owners.Keys);
        if (inputRoot is not null)
        {
            diagnostics = diagnostics.Concat(FindExtraSources(manifest.Profile.Inputs, inputRoot));
        }

        return new(inputs, [.. diagnostics]);
    }

    /// <summary>
    /// 公式入力の一覧と生バイト列のSHA-256を固定値と照合する。改行や文字コードは正規化しない。
    /// </summary>
    /// <param name="inputs">固定した全入力</param>
    /// <param name="inputRoot">入力の相対pathの基準となるtest/coreの配置</param>
    internal static ImmutableArray<CorpusDiagnostic> VerifySources(
        IEnumerable<SourceInput> inputs,
        string inputRoot
    )
    {
        return
        [
            .. inputs.Select(x => CheckSource(x, inputRoot)).OfType<CorpusDiagnostic>(),
            .. FindExtraSources(inputs, inputRoot),
        ];
    }

    /// <summary>
    /// JSONから列挙したcommand一覧と、JSONの親を基準に解決した素材参照を保存用の記録へ写す。
    /// </summary>
    /// <param name="document">列挙済みのJSON</param>
    /// <param name="jsonPath">JSONのmanifest基準の相対path</param>
    internal static ScriptArtifact CreateScript(ScriptDocument document, string jsonPath)
    {
        return new ScriptArtifact
        {
            SourceFilename = document.SourceFilename,
            EnumerationComplete = document.EnumerationComplete,
            CommandCount = document.CommandCount,
            Commands =
            [
                .. document.Commands.Select(x => new ArtifactCommand(
                    x.Index,
                    x.Line,
                    x.Type,
                    x.ModuleType,
                    x.Filename
                )),
            ],
            // 素材領域外を指す参照名は対応する生成物がないため、参照一覧へ含めない。
            References =
            [
                .. document
                    .Commands.Select(x =>
                        x.Filename is { } filename
                        && ResolveReference(jsonPath, filename) is { } path
                            ? new ArtifactReference(x.Index, filename, path)
                            : null
                    )
                    .OfType<ArtifactReference>(),
            ],
        };
    }

    /// <summary>
    /// 入力の相対pathから、素材領域内で同じ相対配置になるJSONのmanifest基準の相対pathを返す。
    /// </summary>
    /// <param name="inputPath">test/core基準の/区切り相対path</param>
    internal static string GetScriptPath(string inputPath)
    {
        return $"{MATERIAL_ROOT}/{Path.ChangeExtension(inputPath, ".json")}";
    }

    /// <summary>
    /// 生成物の拡張子から素材の種類を決める。対応しない拡張子ではnullを返す。
    /// </summary>
    /// <param name="path">/区切りの相対path</param>
    internal static ArtifactKind? GetKind(string path)
    {
        return Path.GetExtension(path) switch
        {
            ".json" => ArtifactKind.Json,
            ".wasm" => ArtifactKind.Wasm,
            ".wat" => ArtifactKind.Wat,
            _ => null,
        };
    }

    private static InputVerification VerifyInput(
        InputConversionResult input,
        string root,
        VerificationMode mode,
        string? inputRoot,
        Dictionary<string, int> recordCounts,
        Dictionary<string, string> owners
    )
    {
        var issues = ImmutableArray.CreateBuilder<CorpusDiagnostic>();
        if (mode == VerificationMode.Execution && input.Status != ConversionStatus.Succeeded)
        {
            issues.Add(
                new(
                    OPERATION,
                    $"manifestの変換状態が{FormatStatus(input.Status)}であり、変換に成功した入力ではありません。",
                    input.Input.Path
                )
            );
        }

        if (inputRoot is not null && CheckSource(input.Input, inputRoot) is { } sourceIssue)
        {
            issues.Add(sourceIssue);
        }

        var checks = input
            .Artifacts.Select(x => CheckArtifact(x, input.Input.Path, root, recordCounts))
            .ToArray();
        var document = ReadDocument(input.Input, checks, issues);
        if (document is null)
        {
            // 実行しない入力では、module素材の異常も入力異常として残す。
            issues.AddRange(
                checks
                    .Where(x => x.Artifact.Kind != ArtifactKind.Json)
                    .Select(x => x.Issue)
                    .OfType<CorpusDiagnostic>()
            );
            return new(input.Input, null, issues.ToImmutable(), [], []);
        }

        var jsonPath = GetScriptPath(input.Input.Path);
        var checksByPath = checks
            .DistinctBy(x => x.Artifact.Path, StringComparer.Ordinal)
            .ToDictionary(x => x.Artifact.Path, StringComparer.Ordinal);
        var references = document
            .Commands.Where(x => x.Filename is not null)
            .Select(x => (Command: x, Path: ResolveReference(jsonPath, x.Filename!)))
            .ToArray();
        var referenceCounts = references
            .Where(x => x.Path is not null)
            .CountBy(x => x.Path!, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
        var modules = ImmutableDictionary.CreateBuilder<int, ImmutableArray<byte>>();
        var moduleIssues = ImmutableDictionary.CreateBuilder<int, CorpusDiagnostic>();
        foreach (var (command, path) in references)
        {
            var check = path is null ? null : checksByPath.GetValueOrDefault(path);
            // 実行処理と同じmodule_typeで素材の種類を決め、binaryのcommandは照合済みbinaryか理由のどちらかを必ず持つ。
            var text = command.ModuleType == "text";
            var expectedKind = text ? ArtifactKind.Wat : ArtifactKind.Wasm;
            var issue = (path, check) switch
            {
                (null, _) => new CorpusDiagnostic(
                    OPERATION,
                    $"#{command.Index}の参照名{command.Filename}は素材領域内の相対pathではありません。",
                    jsonPath
                ),
                (_, null) => new CorpusDiagnostic(
                    OPERATION,
                    owners.TryGetValue(path, out var owner)
                        ? $"#{command.Index}が別の入力{owner}の素材を参照しています。"
                        : $"#{command.Index}がmanifestに記録されていない素材を参照しています。",
                    path
                ),
                _ when referenceCounts[path] > 1 => new CorpusDiagnostic(
                    OPERATION,
                    $"{referenceCounts[path]}件のcommandから参照されており、commandと1対1に対応しません。",
                    path
                ),
                _ when check.Artifact.Kind != expectedKind => new CorpusDiagnostic(
                    OPERATION,
                    $"#{command.Index}は{FormatKind(expectedKind)}の素材を参照する必要がありますが、{FormatKind(check.Artifact.Kind)}として記録されています。",
                    path
                ),
                _ => check.Issue,
            };
            // watは実行処理で開かないため、異常をcommandではなく入力へ残す。
            if (issue is not null && text)
            {
                issues.Add(issue);
            }
            else if (issue is not null)
            {
                moduleIssues[command.Index] = issue;
            }
            else if (!text)
            {
                modules[command.Index] = check!.Content;
            }
        }

        foreach (
            var check in checks.Where(x =>
                x.Artifact.Kind != ArtifactKind.Json
                && !referenceCounts.ContainsKey(x.Artifact.Path)
            )
        )
        {
            issues.Add(
                new(OPERATION, "どのcommandからも参照されていない素材です。", check.Artifact.Path)
            );
            if (check.Issue is { } issue)
            {
                issues.Add(issue);
            }
        }

        return new(
            input.Input,
            document,
            issues.ToImmutable(),
            modules.ToImmutable(),
            moduleIssues.ToImmutable()
        );
    }

    private static ScriptDocument? ReadDocument(
        SourceInput input,
        ArtifactCheck[] checks,
        ImmutableArray<CorpusDiagnostic>.Builder issues
    )
    {
        var scripts = checks.Where(x => x.Artifact.Kind == ArtifactKind.Json).ToArray();
        if (scripts is not [var script])
        {
            issues.Add(
                new(
                    OPERATION,
                    scripts.Length == 0
                        ? "JSONの生成物がありません。"
                        : $"JSONの生成物が{scripts.Length}件記録されています。",
                    input.Path
                )
            );
            return null;
        }

        var expectedPath = GetScriptPath(input.Path);
        if (script.Artifact.Path != expectedPath)
        {
            issues.Add(
                new(
                    OPERATION,
                    $"JSONの生成物は{expectedPath}に配置する必要があります。",
                    script.Artifact.Path
                )
            );
            return null;
        }

        // hashが一致しないJSONは内容を信用できないため、実行せずcommand件数を未確定にする。
        if (script.Issue is { } issue)
        {
            issues.Add(issue);
            return null;
        }

        var document = ScriptDocument.Parse(script.Content.AsSpan(), script.Artifact.Path);
        issues.AddRange(document.Diagnostics);
        if (document.SourceFilename is { } sourceFilename && sourceFilename != input.Path)
        {
            issues.Add(
                new(
                    OPERATION,
                    $"source_filename {sourceFilename}が入力の相対path{input.Path}と一致しません。",
                    script.Artifact.Path
                )
            );
        }

        // 本来のcommand一覧はmanifestに保存した記録で確定するため、JSONと食い違えば実行対象にしない。
        if (!Matches(script.Artifact.Script, CreateScript(document, script.Artifact.Path)))
        {
            issues.Add(
                new(
                    OPERATION,
                    "manifestに記録したcommand一覧または素材参照が生成JSONと一致しません。",
                    script.Artifact.Path
                )
            );
            return null;
        }

        return document;
    }

    private static ArtifactCheck CheckArtifact(
        Artifact artifact,
        string inputPath,
        string root,
        Dictionary<string, int> recordCounts
    )
    {
        var path = artifact.Path;
        if (!IsMaterialPath(path))
        {
            return new(
                artifact,
                [],
                new(OPERATION, $"素材領域{MATERIAL_ROOT}/内の/区切り相対pathではありません。", path)
            );
        }

        if (GetKind(path) != artifact.Kind)
        {
            return new(
                artifact,
                [],
                new(
                    OPERATION,
                    $"拡張子が記録した素材の種類{FormatKind(artifact.Kind)}と対応しません。",
                    path
                )
            );
        }

        if (recordCounts[path] > 1)
        {
            return new(
                artifact,
                [],
                new(
                    OPERATION,
                    $"同じ素材がmanifestに{recordCounts[path]}件記録されています。",
                    path
                )
            );
        }

        if (artifact.InputPath != inputPath)
        {
            return new(
                artifact,
                [],
                new(
                    OPERATION,
                    $"{inputPath}の素材として記録されていますが、所有入力は{artifact.InputPath}です。",
                    path
                )
            );
        }

        byte[] content;
        try
        {
            if (FindLink(root, path) is { } link)
            {
                return new(
                    artifact,
                    [],
                    new(OPERATION, $"素材root内のリンク{link}を経由しています。", path)
                );
            }

            var fullPath = Path.Combine(root, path);
            if (!File.Exists(fullPath))
            {
                return new(artifact, [], new(OPERATION, "素材がありません。", path));
            }

            content = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(
                artifact,
                [],
                new(OPERATION, $"素材を読み取れません: {ex.Message}", path, ex.GetType().FullName)
            );
        }

        var hash = Sha256(content);
        return hash == artifact.Sha256
            ? new(artifact, [.. content], null)
            : new(
                artifact,
                [],
                new(
                    OPERATION,
                    $"SHA-256が記録と一致しません。記録は{artifact.Sha256}、実際は{hash}です。",
                    path
                )
            );
    }

    private static CorpusDiagnostic? CheckSource(SourceInput input, string inputRoot)
    {
        var fullPath = Path.Combine(inputRoot, input.Path);
        byte[] content;
        try
        {
            if (!File.Exists(fullPath))
            {
                return new(OPERATION, "元入力がありません。", input.Path);
            }

            content = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(
                OPERATION,
                $"元入力を読み取れません: {ex.Message}",
                input.Path,
                ex.GetType().FullName
            );
        }

        var hash = Sha256(content);
        return hash == input.Sha256
            ? null
            : new(
                OPERATION,
                $"元入力の生バイト列のSHA-256が固定profileと一致しません。固定値は{input.Sha256}、実際は{hash}です。改行変換を無効にして取得した入力を使用してください。",
                input.Path
            );
    }

    private static List<CorpusDiagnostic> FindExtraSources(
        IEnumerable<SourceInput> inputs,
        string inputRoot
    )
    {
        if (!Directory.Exists(inputRoot))
        {
            return [];
        }

        var expected = inputs.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
        try
        {
            // 遅延列挙の途中で発生する例外も診断にするため、ここで列挙を完了させる。
            return
            [
                .. Directory
                    .EnumerateFiles(inputRoot, "*.wast", SearchOption.AllDirectories)
                    .Select(x =>
                        Path.GetRelativePath(inputRoot, x).Replace(Path.DirectorySeparatorChar, '/')
                    )
                    .Where(x =>
                        x.EndsWith(".wast", StringComparison.Ordinal) && !expected.Contains(x)
                    )
                    .Order(StringComparer.Ordinal)
                    .Select(x => new CorpusDiagnostic(
                        OPERATION,
                        "固定profileにない元入力があります。",
                        x
                    )),
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return
            [
                new(
                    OPERATION,
                    $"元入力の一覧を列挙できません: {ex.Message}",
                    ExceptionType: ex.GetType().FullName
                ),
            ];
        }
    }

    private static List<CorpusDiagnostic> FindExtraMaterials(string root, IEnumerable<string> paths)
    {
        var known = paths.Where(IsMaterialPath).ToHashSet(StringComparer.Ordinal);
        // 記録済みの素材を含むディレクトリのリンクは、各素材の照合で理由を残す。
        var directories = known.SelectMany(GetParentPaths).ToHashSet(StringComparer.Ordinal);
        List<CorpusDiagnostic> diagnostics = [];
        try
        {
            Walk(new DirectoryInfo(Path.Combine(root, MATERIAL_ROOT)), MATERIAL_ROOT);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(
                new(
                    OPERATION,
                    $"素材領域を列挙できません: {ex.Message}",
                    MATERIAL_ROOT,
                    ex.GetType().FullName
                )
            );
        }

        return [.. diagnostics.OrderBy(x => x.Path, StringComparer.Ordinal)];

        void Walk(DirectoryInfo directory, string path)
        {
            if (directory.LinkTarget is not null)
            {
                if (!directories.Contains(path))
                {
                    diagnostics.Add(
                        new(OPERATION, "manifestに記録されていないリンクがあります。", path)
                    );
                }

                return;
            }

            if (!directory.Exists)
            {
                return;
            }

            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                var entryPath = $"{path}/{entry.Name}";
                if (entry is DirectoryInfo child)
                {
                    Walk(child, entryPath);
                }
                else if (entry.LinkTarget is not null && !known.Contains(entryPath))
                {
                    diagnostics.Add(
                        new(OPERATION, "manifestに記録されていないリンクがあります。", entryPath)
                    );
                }
                else if (!known.Contains(entryPath))
                {
                    diagnostics.Add(
                        new(OPERATION, "manifestに記録されていない素材があります。", entryPath)
                    );
                }
            }
        }
    }

    private static IEnumerable<string> GetParentPaths(string path)
    {
        for (var i = path.IndexOf('/'); i >= 0; i = path.IndexOf('/', i + 1))
        {
            yield return path[..i];
        }
    }

    private static string? FindLink(string root, string path)
    {
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var relative = string.Join('/', segments.Take(i + 1));
            var fullPath = Path.Combine(root, relative);
            FileSystemInfo entry =
                i < segments.Length - 1 ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
            if (entry.LinkTarget is not null)
            {
                return relative;
            }
        }

        return null;
    }

    private static string? ResolveReference(string jsonPath, string filename)
    {
        if (!IsRelativePath(filename))
        {
            return null;
        }

        var path = $"{jsonPath[..jsonPath.LastIndexOf('/')]}/{filename}";
        return IsMaterialPath(path) ? path : null;
    }

    private static bool IsMaterialPath(string path)
    {
        return path.StartsWith($"{MATERIAL_ROOT}/", StringComparison.Ordinal)
            && IsRelativePath(path);
    }

    private static bool IsRelativePath(string path)
    {
        // 区切りを/に固定し、絶対path・ドライブ指定・root外への移動・空の要素を受け付けない。
        return path.Length > 0
            && !path.Any(x => x is '\\' or ':' || char.IsControl(x))
            && path.Split('/').All(x => x is not ("" or "." or ".."));
    }

    private static bool Matches(ScriptArtifact? recorded, ScriptArtifact actual)
    {
        return recorded is not null
            && recorded.SourceFilename == actual.SourceFilename
            && recorded.EnumerationComplete == actual.EnumerationComplete
            && recorded.CommandCount == actual.CommandCount
            && recorded.Commands.SequenceEqual(actual.Commands)
            && recorded.References.SequenceEqual(actual.References);
    }

    private static string FormatStatus(ConversionStatus status)
    {
        return status switch
        {
            ConversionStatus.Unprocessed => "unprocessed",
            ConversionStatus.Succeeded => "succeeded",
            _ => "runner_error",
        };
    }

    private static string FormatKind(ArtifactKind kind)
    {
        return kind switch
        {
            ArtifactKind.Json => "json",
            ArtifactKind.Wasm => "wasm",
            _ => "wat",
        };
    }

    private static string Sha256(byte[] content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    /// <summary>
    /// 一つの記録済み素材の照合結果
    /// </summary>
    /// <param name="Artifact">照合した記録</param>
    /// <param name="Content">照合に成功した生バイト列。失敗時は空</param>
    /// <param name="Issue">照合に失敗した理由。成功時はnull</param>
    private sealed record ArtifactCheck(
        Artifact Artifact,
        ImmutableArray<byte> Content,
        CorpusDiagnostic? Issue
    );
}
