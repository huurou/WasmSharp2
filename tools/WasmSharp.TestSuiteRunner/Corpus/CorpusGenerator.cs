using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 固定profileの全入力を固定wast2jsonで変換し、変換状態と生成物をmanifestへ記録する
/// </summary>
/// <remarks>
/// 固定ソースの取得・ビルド・更新は行わず、公式入力と外部ソースを変更しない。
/// </remarks>
internal static class CorpusGenerator
{
    /// <summary>
    /// 出力先に保存するmanifestのファイル名
    /// </summary>
    internal const string MANIFEST_FILE_NAME = "manifest.json";

    private const string PREPARE = "prepare";
    private const string CONVERT = "convert";
    private const string RECORD = "record";

    /// <summary>
    /// 生成前提を確認し、成立した場合だけ全入力をOrdinal順に変換して、変換状態と生成物をmanifestへ確定保存する。
    /// </summary>
    /// <remarks>
    /// 変換器の終了成功、JSONの全command列挙、全参照素材の照合がそろった入力だけを変換成功とする。
    /// 変換成功はランタイムの適合結果ではない。
    /// </remarks>
    /// <param name="request">CLIで解決済みの配置を持つ要求</param>
    internal static GenerateResult Generate(GenerateRequest request)
    {
        var manifestPath = Path.Combine(request.OutputRoot, MANIFEST_FILE_NAME);
        var preconditions = CheckPreconditions(request);
        var manifest = CorpusManifest.Create(request.Profile, preconditions.Provenance);
        if (!preconditions.Diagnostics.IsEmpty)
        {
            // 前提が揃わないまま生成した素材を残さないよう、変換を開始せず全入力を未処理のまま記録する。
            return Save(
                manifest with
                {
                    Diagnostics = [.. preconditions.Diagnostics],
                },
                manifestPath
            );
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (
            var index in Enumerable
                .Range(0, manifest.Inputs.Count)
                .OrderBy(x => manifest.Inputs[x].Input.Path, StringComparer.Ordinal)
        )
        {
            manifest.Inputs[index] = Record(
                request.OutputRoot,
                Convert(request, manifest.Inputs[index]),
                seen
            );
        }

        // 後続の入力による上書きや素材領域の余剰も検出するよう、全入力の変換後にまとめて照合する。
        var verification = CorpusVerifier.Verify(
            manifest,
            manifestPath,
            VerificationMode.Generation,
            request.SpecRoot
        );
        for (var i = 0; i < manifest.Inputs.Count; i++)
        {
            manifest.Inputs[i] = Complete(manifest.Inputs[i], verification.Inputs[i]);
        }

        return Save(manifest with { Diagnostics = [.. verification.Diagnostics] }, manifestPath);
    }

    /// <summary>
    /// 固定入力の一覧と生バイトhash、必要なGit HEAD、変換器の実行ファイルを確認する。
    /// </summary>
    /// <remarks>
    /// spec-root自体がcheckoutの場合だけHEADを照合し、管理外のコピーは全入力の一覧とhashの一致で受け付ける。
    /// 実際のoriginは参考出典として記録し、profileに記録した上流URLとの違いだけでは拒否しない。
    /// </remarks>
    /// <param name="request">CLIで解決済みの配置を持つ要求</param>
    internal static GenerationPreconditions CheckPreconditions(GenerateRequest request)
    {
        var profile = request.Profile;
        var diagnostics = ImmutableArray.CreateBuilder<CorpusDiagnostic>();
        diagnostics.AddRange(
            CorpusVerifier.VerifySources(
                profile.Inputs,
                Path.Combine(request.SpecRoot, profile.WorkingDirectory)
            )
        );
        // 親ディレクトリの無関係なGitをspecの版として扱わないよう、spec-root直下のGit情報だけを見る。
        var specCheckout = IsCheckout(request.SpecRoot);
        var specHead = specCheckout
            ? ReadHead(request.SpecRoot, "spec", profile.Spec, diagnostics)
            : null;
        string? wabtHead = null;
        if (IsCheckout(request.WabtRoot))
        {
            wabtHead = ReadHead(request.WabtRoot, "WABT", profile.Wabt, diagnostics);
        }
        else
        {
            diagnostics.Add(
                new(
                    PREPARE,
                    $"WABTの配置{request.WabtRoot}は、固定commitを確認できるGitのcheckoutではありません。"
                )
            );
        }

        return new(
            new ConversionProvenance
            {
                ExecutableSha256 = HashExecutable(request.ConverterPath, diagnostics),
                ExecutablePath = request.ConverterPath,
                CreatedAt = DateTimeOffset.UtcNow,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                SpecRoot = request.SpecRoot,
                WabtRoot = request.WabtRoot,
                OutputRoot = request.OutputRoot,
                SpecOrigin = specCheckout ? ReadOrigin(request.SpecRoot) : null,
                WabtOrigin = wabtHead is null ? null : ReadOrigin(request.WabtRoot),
                SpecHead = specHead,
                WabtHead = wabtHead,
            },
            diagnostics.ToImmutable()
        );
    }

    /// <summary>
    /// 一つの入力を変換器で変換し、実際の起動引数・終了値・標準出力・標準エラーを記録する。
    /// </summary>
    /// <remarks>
    /// 作業ディレクトリをtest/coreの配置とし、入力は/区切りの相対path、出力はCLIで解決済みの絶対pathで渡す。
    /// シェルを介さず起動し、feature変更などの追加引数を渡さない。
    /// </remarks>
    /// <param name="request">CLIで解決済みの配置を持つ要求</param>
    /// <param name="input">変換する入力の記録</param>
    internal static InputConversionResult Convert(
        GenerateRequest request,
        InputConversionResult input
    )
    {
        var outputPath = Path.GetFullPath(
            Path.Combine(request.OutputRoot, CorpusVerifier.GetScriptPath(input.Input.Path))
        );
        string[] arguments = [input.Input.Path, "-o", outputPath];
        var startInfo = new ProcessStartInfo(request.ConverterPath)
        {
            WorkingDirectory = Path.Combine(request.SpecRoot, request.Profile.WorkingDirectory),
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var started = input with { Arguments = [.. arguments] };
        var directory = Path.GetDirectoryName(outputPath)!;
        try
        {
            // 変換器は出力先のディレクトリを作成しないため、入力と同じ相対配置を先に用意する。
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(started, $"出力先{directory}を用意できません: {ex.Message}", ex);
        }

        (int ExitCode, string Output, string Error) run;
        try
        {
            run = RunProcess(startInfo);
        }
        catch (Win32Exception ex)
        {
            return Fail(started, $"変換器を起動できません: {ex.Message}", ex);
        }

        var result = started with
        {
            ExitCode = run.ExitCode,
            StandardOutput = run.Output,
            StandardError = run.Error,
        };
        return run.ExitCode == 0
            ? result with
            {
                Status = ConversionStatus.Succeeded,
            }
            : Fail(result, $"変換器が終了値{run.ExitCode}で終了しました。", null);
    }

    private static InputConversionResult Fail(
        InputConversionResult input,
        string message,
        Exception? exception
    )
    {
        return input with
        {
            Status = ConversionStatus.RunnerError,
            Diagnostics =
            [
                .. input.Diagnostics,
                new(CONVERT, message, input.Input.Path, exception?.GetType().FullName),
            ],
        };
    }

    private static InputConversionResult Record(
        string outputRoot,
        InputConversionResult input,
        HashSet<string> seen
    )
    {
        // 変換器はJSONと同じディレクトリへ素材を書くため、そこで新たに現れたファイルをこの入力の生成物とする。
        var scriptPath = CorpusVerifier.GetScriptPath(input.Input.Path);
        var directory = scriptPath[..scriptPath.LastIndexOf('/')];
        var fullDirectory = Path.Combine(outputRoot, directory);
        List<Artifact> artifacts = [];
        List<CorpusDiagnostic> diagnostics = [];
        try
        {
            var created = Directory.Exists(fullDirectory)
                ? Directory
                    .EnumerateFiles(fullDirectory)
                    .Select(x => $"{directory}/{Path.GetFileName(x)}")
                    .Where(seen.Add)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : [];
            foreach (var path in created)
            {
                if (CorpusVerifier.GetKind(path) is not { } kind)
                {
                    diagnostics.Add(new(RECORD, "素材として扱わない種類の生成物です。", path));
                    continue;
                }

                var content = File.ReadAllBytes(Path.Combine(outputRoot, path));
                artifacts.Add(
                    new(
                        path,
                        kind,
                        System.Convert.ToHexStringLower(SHA256.HashData(content)),
                        input.Input.Path
                    )
                    {
                        Script =
                            kind == ArtifactKind.Json
                                ? CorpusVerifier.CreateScript(
                                    ScriptDocument.Parse(content, path),
                                    path
                                )
                                : null,
                    }
                );
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(
                new(
                    RECORD,
                    $"生成物を記録できません: {ex.Message}",
                    input.Input.Path,
                    ex.GetType().FullName
                )
            );
        }

        return input with
        {
            Artifacts = [.. input.Artifacts, .. artifacts],
            Diagnostics = [.. input.Diagnostics, .. diagnostics],
        };
    }

    private static InputConversionResult Complete(
        InputConversionResult input,
        InputVerification verification
    )
    {
        var issues = verification
            .Issues.Concat(verification.ModuleIssues.OrderBy(x => x.Key).Select(x => x.Value))
            .ToArray();
        // 終了成功に加え、JSONの全command列挙と全参照素材の照合がそろった入力だけを変換成功とする。
        // 一部の生成物だけがそろった入力も、部分生成物を残したまま失敗として記録する。
        if (
            input.Status == ConversionStatus.Succeeded
            && input.Diagnostics.Count == 0
            && issues.Length == 0
            && verification.Document is { EnumerationComplete: true }
        )
        {
            return input;
        }

        return input with
        {
            Status = ConversionStatus.RunnerError,
            Diagnostics = [.. input.Diagnostics, .. issues],
        };
    }

    private static GenerateResult Save(CorpusManifest manifest, string manifestPath)
    {
        var recorded = manifest with
        {
            Summary = manifest.Summarize(),
            Completion = new(
                manifest.Inputs.All(x => x.Status != ConversionStatus.Unprocessed),
                false
            ),
        };
        // 保存する内容には出力の完了を記録し、確定保存に失敗した場合だけ未完了のまま返す。
        var saved = recorded with
        {
            Completion = recorded.Completion with { OutputComplete = true },
        };
        try
        {
            // 未作成の出力先も受け付けるため、前提の不成立を記録する場合も保存先を用意する。
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(
                recorded,
                manifestPath,
                new($"{manifestPath}の保存先を用意できません: {ex.Message}", manifestPath, ex)
            );
        }

        try
        {
            ReportStore.Save(saved, manifestPath);
            return new(saved, manifestPath, null);
        }
        catch (ReportStoreException ex)
        {
            return new(recorded, manifestPath, ex);
        }
    }

    private static bool IsCheckout(string root)
    {
        return Path.Exists(Path.Combine(root, ".git"));
    }

    private static string? ReadHead(
        string root,
        string name,
        SourceRevision revision,
        ImmutableArray<CorpusDiagnostic>.Builder diagnostics
    )
    {
        var (exitCode, output, error) = RunGit(root, "rev-parse", "--verify", "HEAD");
        if (exitCode != 0)
        {
            diagnostics.Add(
                new(
                    PREPARE,
                    $"{name}の配置{root}からGitのHEADを取得できません。終了値: {exitCode?.ToString() ?? "なし"}、標準エラー: {error.Trim()}"
                )
            );
            return null;
        }

        var head = output.Trim();
        if (head != revision.Commit)
        {
            diagnostics.Add(
                new(
                    PREPARE,
                    $"{name}の配置{root}のHEAD {head}が固定commit {revision.Commit}と一致しません。"
                )
            );
        }

        return head;
    }

    private static string? ReadOrigin(string root)
    {
        return RunGit(root, "remote", "get-url", "origin") is (0, var output, _)
            ? output.Trim()
            : null;
    }

    private static (int? ExitCode, string Output, string Error) RunGit(
        string root,
        params string[] arguments
    )
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var startInfo = new ProcessStartInfo("git");
        // 呼出し元の環境変数で別のリポジトリを参照せず、指定した配置より上の無関係なGitも探索しない。
        startInfo.Environment.Remove("GIT_DIR");
        startInfo.Environment.Remove("GIT_WORK_TREE");
        startInfo.Environment["GIT_CEILING_DIRECTORIES"] =
            Path.GetDirectoryName(fullRoot) ?? fullRoot;
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(root);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            return RunProcess(startInfo);
        }
        catch (Win32Exception ex)
        {
            // Gitを起動できない場合は、取得できない情報として理由とともに呼出し元へ返す。
            return (null, string.Empty, $"gitを起動できません: {ex.Message}");
        }
    }

    private static string? HashExecutable(
        string path,
        ImmutableArray<CorpusDiagnostic>.Builder diagnostics
    )
    {
        try
        {
            using var stream = File.OpenRead(path);
            return System.Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(
                new(
                    PREPARE,
                    $"変換器の実行ファイル{path}を読み取れません: {ex.Message}",
                    ExceptionType: ex.GetType().FullName
                )
            );
            return null;
        }
    }

    private static (int ExitCode, string Output, string Error) RunProcess(
        ProcessStartInfo startInfo
    )
    {
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        using var process = Process.Start(startInfo)!;
        // 片方の出力でパイプが詰まって待ち続けないよう、標準出力と標準エラーを同時に回収する。
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
