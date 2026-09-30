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
    /// <summary>
    /// 入力・素材・参照の照合失敗を診断で識別する操作名
    /// </summary>
    private const string OPERATION = "verify";

    /// <summary>
    /// manifestの親を基準に生成物を配置する素材領域の名前
    /// </summary>
    private const string MATERIAL_ROOT = "modules";

    /// <summary>
    /// manifestに記録した素材を、manifestの親を基準にした相対配置で照合する。
    /// </summary>
    /// <param name="manifest">照合するmanifest</param>
    /// <param name="manifestPath">manifestの配置先 親ディレクトリを素材の基準にする</param>
    /// <param name="mode">元入力も照合するかどうかを決める工程</param>
    /// <param name="sourceRoot">生成工程で照合する公式入力のspec-root 実行工程では使用しない</param>
    /// <returns>manifestの記録順の入力別照合結果と、どの入力にも属さない余剰素材・元入力の診断</returns>
    /// <exception cref="ArgumentNullException">生成工程でsourceRootがnullの場合</exception>
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
    /// <returns>入力の欠落・読取失敗・hash不一致と、固定集合にない入力や列挙失敗の診断 問題がなければ空</returns>
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
    /// <returns>command一覧をコピーした保存用記録 素材領域内へ解決できない参照名は参照一覧へ含めない</returns>
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
    /// <returns>modules/配下で入力と同じ相対配置にあり、拡張子を.jsonにしたpath</returns>
    internal static string GetScriptPath(string inputPath)
    {
        return $"{MATERIAL_ROOT}/{Path.ChangeExtension(inputPath, ".json")}";
    }

    /// <summary>
    /// 生成物の拡張子から素材の種類を決める。対応しない拡張子ではnullを返す。
    /// </summary>
    /// <param name="path">/区切りの相対path</param>
    /// <returns>.json・.wasm・.watに対応する素材の種類 大文字を含む拡張子や未対応の拡張子ではnull</returns>
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

    /// <summary>
    /// 一つの入力のJSONと参照素材を照合し、入力全体の異常とbinaryを使うcommandごとの異常を分ける。
    /// </summary>
    /// <param name="input">照合する元入力と生成物の記録</param>
    /// <param name="root">manifestの親である素材配置の基準</param>
    /// <param name="mode">変換成功の記録を実行前提として要求するかどうかを決める工程</param>
    /// <param name="inputRoot">元入力を照合するtest/coreの配置 元入力を照合しない場合はnull</param>
    /// <param name="recordCounts">素材pathごとの全入力を通じた記録数</param>
    /// <param name="owners">各素材pathを記録している入力の相対path</param>
    /// <returns>照合できたJSON・binaryのバイト列と、入力全体または参照元commandに対応する失敗理由</returns>
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

    /// <summary>
    /// 唯一のJSON生成物の配置・hash・manifestの列挙記録を照合し、信頼できる内容だけを返す。
    /// </summary>
    /// <remarks>
    /// source_filenameの不一致やJSONの列挙・全体構造の異常は診断へ残し、manifestの記録と一致する場合は内容を返す。
    /// </remarks>
    /// <param name="input">JSONに対応する元入力</param>
    /// <param name="checks">この入力に属する全生成物の照合結果</param>
    /// <param name="issues">JSONの配置・内容・記録に異常がある場合に理由を追加する診断一覧</param>
    /// <returns>hashと列挙記録が一致するJSON JSONを一意に特定できないか、配置・hash・記録が不正な場合はnull</returns>
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

    /// <summary>
    /// 素材領域内の配置・拡張子・記録の一意性・所有元・リンク経由の有無・hashを照合する。
    /// </summary>
    /// <param name="artifact">照合する生成物の記録</param>
    /// <param name="inputPath">この素材を所有するはずの元入力の相対path</param>
    /// <param name="root">manifestの親である素材配置の基準</param>
    /// <param name="recordCounts">素材pathごとの全入力を通じた記録数</param>
    /// <returns>成功時は読み取ったバイト列、失敗時は空のバイト列と最初に判明した理由を持つ照合結果</returns>
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

    /// <summary>
    /// 元入力の生バイト列を読み取り、固定profileのSHA-256と照合する。
    /// </summary>
    /// <param name="input">元入力の相対pathと固定SHA-256</param>
    /// <param name="inputRoot">元入力を配置したtest/coreのディレクトリ</param>
    /// <returns>欠落・読取失敗・hash不一致の診断 一致する場合はnull</returns>
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

    /// <summary>
    /// test/core以下の.wastを再帰的に列挙し、固定profileにない元入力を検出する。
    /// </summary>
    /// <param name="inputs">固定profileに含まれる全入力</param>
    /// <param name="inputRoot">列挙するtest/coreの配置</param>
    /// <returns>余剰入力を相対pathのOrdinal順に並べた診断、または列挙失敗の診断 ディレクトリが存在しない場合は空</returns>
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

    /// <summary>
    /// 素材領域をリンク先へ進まずに列挙し、manifestにない素材とリンクを検出する。
    /// </summary>
    /// <param name="root">manifestの親である素材配置の基準</param>
    /// <param name="paths">manifestに記録された全生成物の相対path</param>
    /// <returns>余剰素材・未記録リンク・列挙失敗の診断を相対pathのOrdinal順に並べた一覧</returns>
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

    /// <summary>
    /// /区切りの素材pathから、途中にある親ディレクトリの相対pathを列挙する。
    /// </summary>
    /// <param name="path">素材のmanifest基準の相対path</param>
    /// <returns>最上位から順に並べた親ディレクトリの相対path 素材自身は含めない</returns>
    private static IEnumerable<string> GetParentPaths(string path)
    {
        for (var i = path.IndexOf('/'); i >= 0; i = path.IndexOf('/', i + 1))
        {
            yield return path[..i];
        }
    }

    /// <summary>
    /// 素材までの各path要素を確認し、リンクを経由する最初の箇所を探す。
    /// </summary>
    /// <param name="root">manifestの親である素材配置の基準</param>
    /// <param name="path">確認する素材の/区切り相対path</param>
    /// <returns>最初に見つかったリンクの相対path リンクを経由しない場合はnull</returns>
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

    /// <summary>
    /// commandの参照名をJSONの親ディレクトリに対して解決し、素材領域内の相対pathに限定する。
    /// </summary>
    /// <param name="jsonPath">JSONのmanifest基準の相対path</param>
    /// <param name="filename">commandに記録された参照名</param>
    /// <returns>解決した素材の相対path 参照名が不正か、素材領域内に収まらない場合はnull</returns>
    private static string? ResolveReference(string jsonPath, string filename)
    {
        if (!IsRelativePath(filename))
        {
            return null;
        }

        var path = $"{jsonPath[..jsonPath.LastIndexOf('/')]}/{filename}";
        return IsMaterialPath(path) ? path : null;
    }

    /// <summary>
    /// pathがmodules/配下を表す/区切り相対pathであるかを判定する。
    /// </summary>
    /// <param name="path">manifestに記録された素材pathまたは解決した参照先</param>
    /// <returns>modules/で始まり、空・.・..の要素や禁止文字を含まない相対pathの場合はtrue</returns>
    private static bool IsMaterialPath(string path)
    {
        return path.StartsWith($"{MATERIAL_ROOT}/", StringComparison.Ordinal)
            && IsRelativePath(path);
    }

    /// <summary>
    /// /区切り相対pathとして、領域外への移動や曖昧な解釈を生む要素がないかを判定する。
    /// </summary>
    /// <param name="path">確認する相対path</param>
    /// <returns>空・.・..の要素、バックスラッシュ、コロン、制御文字を含まない空でないpathの場合はtrue</returns>
    private static bool IsRelativePath(string path)
    {
        // 区切りを/に固定し、絶対path・ドライブ指定・root外への移動・空の要素を受け付けない。
        return path.Length > 0
            && !path.Any(x => x is '\\' or ':' || char.IsControl(x))
            && path.Split('/').All(x => x is not ("" or "." or ".."));
    }

    /// <summary>
    /// manifestのJSON列挙記録と、実際のJSONから読み取った一覧・素材参照を順序も含めて比較する。
    /// </summary>
    /// <param name="recorded">manifestに保存したJSONの列挙記録 記録がない場合はnull</param>
    /// <param name="actual">照合済みのJSONから作成した列挙記録</param>
    /// <returns>元入力名、列挙完了状態、command総数、command一覧、素材参照がすべて一致する場合はtrue</returns>
    private static bool Matches(ScriptArtifact? recorded, ScriptArtifact actual)
    {
        return recorded is not null
            && recorded.SourceFilename == actual.SourceFilename
            && recorded.EnumerationComplete == actual.EnumerationComplete
            && recorded.CommandCount == actual.CommandCount
            && recorded.Commands.SequenceEqual(actual.Commands)
            && recorded.References.SequenceEqual(actual.References);
    }

    /// <summary>
    /// 入力の変換状態を保存JSONと同じ表記の診断用文字列にする。
    /// </summary>
    /// <param name="status">診断に表示する変換状態</param>
    /// <returns>Unprocessedはunprocessed、Succeededはsucceeded、それ以外はrunner_error</returns>
    private static string FormatStatus(ConversionStatus status)
    {
        return status switch
        {
            ConversionStatus.Unprocessed => "unprocessed",
            ConversionStatus.Succeeded => "succeeded",
            _ => "runner_error",
        };
    }

    /// <summary>
    /// 生成物の種類を保存JSONと同じ表記の診断用文字列にする。
    /// </summary>
    /// <param name="kind">診断に表示する生成物の種類</param>
    /// <returns>Jsonはjson、Wasmはwasm、それ以外はwat</returns>
    private static string FormatKind(ArtifactKind kind)
    {
        return kind switch
        {
            ArtifactKind.Json => "json",
            ArtifactKind.Wasm => "wasm",
            _ => "wat",
        };
    }

    /// <summary>
    /// 生バイト列を正規化せずSHA-256を求める。
    /// </summary>
    /// <param name="content">hashを求める元入力または素材のバイト列</param>
    /// <returns>小文字hex64桁のSHA-256</returns>
    private static string Sha256(byte[] content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    /// <summary>
    /// 一つの記録済み素材の照合結果
    /// </summary>
    /// <param name="Artifact">照合した記録</param>
    /// <param name="Content">照合に成功した生バイト列 失敗時は空</param>
    /// <param name="Issue">照合に失敗した理由 成功時はnull</param>
    private sealed record ArtifactCheck(
        Artifact Artifact,
        ImmutableArray<byte> Content,
        CorpusDiagnostic? Issue
    );
}
