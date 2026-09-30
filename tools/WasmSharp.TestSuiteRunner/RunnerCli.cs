using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner;

/// <summary>
/// 固定profileに従い、指定された一つのCLI操作を実行する
/// </summary>
/// <param name="profile">生成・実行・最終判定の対象となる固定profile</param>
internal sealed class RunnerCli(Core2Profile profile)
{
    /// <summary>
    /// 引数から一つの操作を実行し、終了値を返す。
    /// </summary>
    /// <param name="args">操作名と明示的なpath引数</param>
    /// <param name="output">進捗・集計・判定の表示先</param>
    /// <param name="error">引数・操作・保存の失敗理由の表示先</param>
    /// <returns>成功は0、条件不成立は1、操作未完了は2</returns>
    internal int Run(string[] args, TextWriter output, TextWriter error)
    {
        Dictionary<string, string> paths;
        try
        {
            if (
                args is ["--help"]
                || args is [var command, "--help"] && Options(command) is not null
            )
            {
                WriteHelp(output);
                return 0;
            }
            paths = Parse(args, Environment.CurrentDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            error.WriteLine(ex.Message);
            WriteHelp(error);
            return 2;
        }

        try
        {
            ProtectOutput(args[0], paths);
            return args[0] switch
            {
                "generate" => Generate(paths, output, error),
                "run" => Execute(paths, output, error),
                "baseline-save" => SaveBaseline(paths, output),
                "compare-conversion" or "compare-run" => Compare(args[0], paths, output, error),
                "verify" => Verify(paths["--input"], output),
                _ => throw new InvalidOperationException("解析済みの操作が見つかりません。"),
            };
        }
        catch (Exception ex)
            when (ex
                    is ReportStoreException
                        or IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            error.WriteLine(ex.Message);
            return 2;
        }
    }

    /// <summary>
    /// 操作ごとに要求するpath引数の名前を返す。
    /// </summary>
    /// <param name="operation">操作名</param>
    /// <returns>必須引数の一覧 未知の操作はnull</returns>
    private static string[]? Options(string operation)
    {
        return operation switch
        {
            "generate" => ["--spec-root", "--wabt-root", "--wast2json", "--output"],
            "run" => ["--manifest", "--output"],
            "baseline-save" => ["--input", "--output"],
            "compare-conversion" or "compare-run" => ["--baseline", "--current", "--output"],
            "verify" => ["--input"],
            _ => null,
        };
    }

    /// <summary>
    /// 未知・重複・不足引数を拒否し、全pathを起動時の作業位置で絶対化する。
    /// </summary>
    /// <param name="args">操作名と引数</param>
    /// <param name="workingDirectory">CLIを開始した作業ディレクトリ</param>
    /// <returns>必須引数名と絶対pathの対応</returns>
    /// <exception cref="ArgumentException">操作または引数が不正な場合</exception>
    private static Dictionary<string, string> Parse(string[] args, string workingDirectory)
    {
        var options =
            (args.Length > 0 ? Options(args[0]) : null)
            ?? throw new ArgumentException("有効な操作を指定してください。");
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            var option = args[i];
            if (!options.Contains(option, StringComparer.Ordinal))
            {
                throw new ArgumentException($"未知の引数です: {option}");
            }
            if (
                i + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[i + 1])
                || args[i + 1].StartsWith("--", StringComparison.Ordinal)
            )
            {
                throw new ArgumentException($"{option}のpathが不足しています。");
            }
            if (!paths.TryAdd(option, Path.GetFullPath(args[i + 1], workingDirectory)))
            {
                throw new ArgumentException($"引数が重複しています: {option}");
            }
        }
        foreach (var option in options)
        {
            if (!paths.ContainsKey(option))
            {
                throw new ArgumentException($"必須引数が不足しています: {option}");
            }
        }
        return paths;
    }

    /// <summary>
    /// 固定ソース、入力JSON、既存出力を変更する指定を拒否する。
    /// </summary>
    /// <param name="operation">出力を行う操作名 verifyは検査対象外</param>
    /// <param name="paths">絶対化したpath引数</param>
    /// <exception cref="IOException">保護対象と出力先が重なる場合</exception>
    private static void ProtectOutput(string operation, Dictionary<string, string> paths)
    {
        if (!paths.TryGetValue("--output", out var output))
        {
            return;
        }
        var resolvedOutput = ResolvePath(output);
        if (operation == "generate")
        {
            foreach (var option in new[] { "--spec-root", "--wabt-root" })
            {
                var source = ResolvePath(paths[option]);
                if (ContainsPath(source, resolvedOutput) || ContainsPath(resolvedOutput, source))
                {
                    throw new IOException($"生成先{output}は{option}のソース配下または祖先です。");
                }
            }
            if (
                File.Exists(output)
                || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()
            )
            {
                throw new IOException(
                    $"生成先{output}には既存の内容があります。未作成または空の専用ディレクトリを指定してください。"
                );
            }
            return;
        }
        foreach (var option in new[] { "--manifest", "--input", "--baseline", "--current" })
        {
            if (
                paths.TryGetValue(option, out var input)
                && ContainsPath(resolvedOutput, ResolvePath(input))
            )
            {
                throw new IOException($"入力自身を出力先には指定できません: {output}");
            }
        }
        if (operation != "baseline-save" && Path.Exists(output))
        {
            throw new IOException($"既存の出力先は上書きできません: {output}");
        }
    }

    /// <summary>
    /// 存在するpath要素のリンク先を解決し、未作成の末尾も含めて配置を比較できるようにする。
    /// </summary>
    /// <param name="path">絶対path</param>
    /// <returns>リンク経由の別名を解決した絶対path</returns>
    private static string ResolvePath(string path)
    {
        var root = Path.GetPathRoot(path)!;
        var resolved = root;
        foreach (
            var part in path[root.Length..]
                .Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries
                )
        )
        {
            resolved = Path.Combine(resolved, part);
            FileSystemInfo info = Directory.Exists(resolved)
                ? new DirectoryInfo(resolved)
                : new FileInfo(resolved);
            if (info.Exists && info.LinkTarget is not null)
            {
                resolved = info.ResolveLinkTarget(returnFinalTarget: true)!.FullName;
            }
        }
        return Path.TrimEndingDirectorySeparator(resolved);
    }

    /// <summary>
    /// pathの要素境界を保って、同じ配置または配下であるかを判定する。
    /// </summary>
    /// <param name="root">基準の絶対path</param>
    /// <param name="path">比較する絶対path</param>
    /// <returns>同じ配置または配下の場合はtrue</returns>
    private static bool ContainsPath(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(root, path, comparison)
            || path.StartsWith(
                Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar,
                comparison
            );
    }

    /// <summary>
    /// 生成だけを実行し、manifestの集計と保存結果から終了値を返す。
    /// </summary>
    /// <param name="paths">固定ソース、変換器、生成先の絶対path</param>
    /// <param name="output">進捗と集計の表示先</param>
    /// <param name="error">操作と保存の失敗の表示先</param>
    /// <returns>生成操作の終了値</returns>
    private int Generate(Dictionary<string, string> paths, TextWriter output, TextWriter error)
    {
        var processed = 0;
        var result = CorpusGenerator.Generate(
            new(
                profile,
                paths["--spec-root"],
                paths["--wabt-root"],
                paths["--wast2json"],
                paths["--output"]
            ),
            path => output.WriteLine($"[{++processed}/{profile.Inputs.Length}] 変換・記録: {path}")
        );
        WriteConversion(result.Manifest, output);
        foreach (var diagnostic in result.Manifest.Diagnostics)
        {
            error.WriteLine(diagnostic.Message);
        }
        foreach (var input in result.Manifest.Inputs)
        {
            foreach (var diagnostic in input.Diagnostics)
            {
                error.WriteLine($"{input.Input.Path}: {diagnostic.Message}");
            }
        }
        WriteSaveResult(result.ManifestPath, result.SaveFailure, output, error);
        return WriteDecision(
            "生成",
            CompletionPolicy.Generate(
                result.Manifest,
                ReportStore.Validate(result.Manifest),
                result.SaveFailure is null
            ),
            output,
            error
        );
    }

    /// <summary>
    /// 保存済み素材だけを実行し、結果の集計と保存結果から終了値を返す。
    /// </summary>
    /// <param name="paths">manifestと結果の絶対path</param>
    /// <param name="output">進捗と集計の表示先</param>
    /// <param name="error">操作と保存の失敗の表示先</param>
    /// <returns>通常実行の終了値</returns>
    private int Execute(Dictionary<string, string> paths, TextWriter output, TextWriter error)
    {
        var manifest = ReportStore.ReadManifest(paths["--manifest"]).Manifest;
        if (!manifest.Profile.Matches(profile))
        {
            throw new ReportStoreException(
                $"固定profile{profile.Id}と条件・対象入力が一致しないmanifestです。",
                paths["--manifest"]
            );
        }
        var processed = 0;
        var result = SuiteExecutor.ExecuteAndSave(
            manifest,
            paths["--manifest"],
            paths["--output"],
            progress: path =>
                output.WriteLine($"[{++processed}/{profile.Inputs.Length}] 実行・記録: {path}")
        );
        WriteRun(result.Report, output);
        foreach (var diagnostic in result.Report.Diagnostics)
        {
            error.WriteLine(diagnostic.Message);
        }
        WriteSaveResult(result.OutputPath, result.SaveFailure, output, error);
        return WriteDecision(
            "実行",
            CompletionPolicy.Run(
                result.Report,
                ReportStore.Validate(result.Report),
                result.SaveFailure is null
            ),
            output,
            error
        );
    }

    /// <summary>
    /// 完了した保存JSONをそのままbaselineへ明示保存する。
    /// </summary>
    /// <param name="paths">保存元とbaselineの絶対path</param>
    /// <param name="output">保存先の表示先</param>
    /// <returns>保存成功時は0 失敗は呼出側で終了2へ変換する</returns>
    private static int SaveBaseline(Dictionary<string, string> paths, TextWriter output)
    {
        BaselineStore.Save(paths["--input"], paths["--output"]);
        output.WriteLine("baseline保存: 完了");
        output.WriteLine($"保存先: {paths["--output"]}");
        return 0;
    }

    /// <summary>
    /// 保存済みの2結果だけを比較し、比較結果を別のpathへ保存する。
    /// </summary>
    /// <param name="operation">変換比較または実行比較の操作名</param>
    /// <param name="paths">baseline、現結果、比較結果の絶対path</param>
    /// <param name="output">集計と判定の表示先</param>
    /// <param name="error">未完了理由の表示先</param>
    /// <returns>比較操作の終了値</returns>
    private static int Compare(
        string operation,
        Dictionary<string, string> paths,
        TextWriter output,
        TextWriter error
    )
    {
        var report =
            operation == "compare-conversion"
                ? BaselineComparer.CompareConversion(
                    ReportStore.ReadManifest(paths["--baseline"]).Manifest,
                    ReportStore.ReadManifest(paths["--current"]).Manifest
                )
                : BaselineComparer.CompareRun(
                    ReportStore.ReadRunReport(paths["--baseline"]).Report,
                    ReportStore.ReadRunReport(paths["--current"]).Report
                );
        report = report with
        {
            Baseline = report.Baseline with { Path = paths["--baseline"] },
            Current = report.Current with { Path = paths["--current"] },
        };
        ReportStore.Save(report, paths["--output"]);
        output.WriteLine($"比較成立: {(report.Established ? "成立" : "未成立")}");
        output.WriteLine($"比較完了: {(report.Complete ? "完了" : "未完了")}");
        var summary = report.Summary;
        output.WriteLine(
            $"条件差: {summary.ConditionDifferenceCount}件、出典差: {summary.ProvenanceDifferenceCount}件、入力・素材差: {summary.EntryDifferenceCount}件"
        );
        output.WriteLine(
            $"変化: {summary.ChangedCaseCount}件、追加: {summary.AddedCaseCount}件、欠落: {summary.MissingCaseCount}件、回帰: {summary.RegressionCount}件"
        );
        output.WriteLine(
            $"未比較: {summary.UncomparedCount}件、現結果のfailed: {summary.CurrentFailedCount}件、現結果のrunner_error: {summary.CurrentRunnerErrorCount}件"
        );
        foreach (var item in report.Uncompared)
        {
            error.WriteLine(item.Reason);
        }
        output.WriteLine($"保存先: {paths["--output"]}");
        var decision =
            operation == "compare-conversion"
                ? CompletionPolicy.CompareConversion(report, saved: true)
                : CompletionPolicy.CompareRun(report, saved: true);
        return WriteDecision("比較", decision, output, error);
    }

    /// <summary>
    /// 単一の保存済み実行結果を再実行せずに最終判定する。
    /// </summary>
    /// <param name="path">実行結果の絶対path</param>
    /// <param name="output">単一結果の判定と残る理由の表示先</param>
    /// <returns>合格は0、読取可能な結果の条件不成立は1</returns>
    private int Verify(string path, TextWriter output)
    {
        var stored = ReportStore.ReadRunReport(path);
        var decision = CompletionPolicy.Verify(stored.Report, stored.Issues, profile);
        output.WriteLine($"最終判定: {(decision.ExitCode == 0 ? "合格" : "不合格")}");
        foreach (var reason in decision.Reasons)
        {
            output.WriteLine(reason);
        }
        return decision.ExitCode;
    }

    /// <summary>
    /// 変換記録に対応する件数を表示する。
    /// </summary>
    /// <param name="manifest">表示する変換結果</param>
    /// <param name="output">集計の表示先</param>
    private static void WriteConversion(CorpusManifest manifest, TextWriter output)
    {
        var summary = manifest.Summary;
        output.WriteLine(
            $"対象入力: {summary.InputCount}件、処理済み: {summary.SucceededCount + summary.RunnerErrorCount}件、未処理: {summary.UnprocessedCount}件"
        );
        output.WriteLine(
            $"変換成功: {summary.SucceededCount}件、入力異常: {summary.RunnerErrorCount}件、生成物: {summary.ArtifactCount}件"
        );
        var undetermined = manifest.Inputs.Count(x =>
            x.Status != ConversionStatus.Unprocessed
            && !x.Artifacts.Any(y => y.Script is { EnumerationComplete: true })
        );
        output.WriteLine($"command件数未確定: {undetermined}入力");
    }

    /// <summary>
    /// 実行記録に対応する入力・command・分類の件数を表示する。
    /// </summary>
    /// <param name="report">表示する実行結果</param>
    /// <param name="output">集計の表示先</param>
    private static void WriteRun(RunReport report, TextWriter output)
    {
        var summary = report.Summary;
        output.WriteLine(
            $"対象入力: {summary.InputCount}件、処理済み: {summary.ProcessedInputCount}件、中断: {summary.IncompleteInputCount}件、未処理: {summary.UnprocessedInputCount}件、件数未確定: {summary.UndeterminedInputCount}件"
        );
        output.WriteLine(
            $"列挙済みcommand: {summary.EnumeratedCommandCount}件、未処理command: {summary.UnprocessedCommandCount}件"
        );
        output.WriteLine($"入力異常: {summary.InputIssueCount}件（{summary.IssueInputCount}入力）");
        WriteOutcomes("セットアップ", summary.Setup, output);
        WriteOutcomes("単独action", summary.Action, output);
        WriteOutcomes("assertion", summary.Assertion, output);
        output.WriteLine(
            $"種類未確定command: runner_error={summary.UncategorizedRunnerErrorCount}"
        );
    }

    /// <summary>
    /// 一つのcategoryの6分類を表示する。
    /// </summary>
    /// <param name="name">categoryの表示名</param>
    /// <param name="counts">6分類の件数</param>
    /// <param name="output">表示先</param>
    private static void WriteOutcomes(string name, OutcomeCounts counts, TextWriter output)
    {
        output.WriteLine(
            $"{name}: passed={counts.Passed}, failed={counts.Failed}, runtime_unsupported={counts.RuntimeUnsupported}, runner_error={counts.RunnerError}, out_of_scope={counts.OutOfScope}, blocked={counts.Blocked}"
        );
    }

    /// <summary>
    /// 保存成功時だけ保存先を表示し、失敗時はpathと理由を標準エラーへ出す。
    /// </summary>
    /// <param name="path">保存先</param>
    /// <param name="failure">保存失敗 保存成功時はnull</param>
    /// <param name="output">保存先の表示先</param>
    /// <param name="error">失敗理由の表示先</param>
    private static void WriteSaveResult(
        string path,
        ReportStoreException? failure,
        TextWriter output,
        TextWriter error
    )
    {
        if (failure is null)
        {
            output.WriteLine($"保存先: {path}");
        }
        else
        {
            error.WriteLine(failure.Message);
        }
    }

    /// <summary>
    /// 操作の判定と残る理由を表示し、用途別の終了値を返す。
    /// </summary>
    /// <param name="name">操作の表示名</param>
    /// <param name="decision">完了条件と合格条件の判定</param>
    /// <param name="output">判定と用途不合格理由の表示先</param>
    /// <param name="error">操作未完了理由の表示先</param>
    /// <returns>判定が定める0・1・2</returns>
    private static int WriteDecision(
        string name,
        CompletionDecision decision,
        TextWriter output,
        TextWriter error
    )
    {
        output.WriteLine(
            $"{name}判定: {decision.ExitCode switch { 0 => "成立", 1 => "条件不成立", _ => "未完了" }}（終了値{decision.ExitCode}）"
        );
        var writer = decision.ExitCode == 2 ? error : output;
        foreach (var reason in decision.Reasons)
        {
            writer.WriteLine(reason);
        }
        return decision.ExitCode;
    }

    /// <summary>
    /// 個別操作の引数と、初回・後続の操作例を表示する。
    /// </summary>
    /// <param name="output">操作説明の表示先</param>
    private static void WriteHelp(TextWriter output)
    {
        var converter = OperatingSystem.IsWindows()
            ? "artifacts/wabt-core2/Release/wast2json.exe"
            : "artifacts/wabt-core2/wast2json";
        output.WriteLine(
            $$"""
            Test Suite Runner: WebAssembly Core 2.0の公式適合検証ツール
            使用法: WasmSharp.TestSuiteRunner <操作> <引数>
              generate --spec-root <path> --wabt-root <path> --wast2json <path> --output <directory>
                固定全入力を変換・照合し、空の専用領域にmanifest.jsonと素材を保存します。
              run --manifest <json> --output <json>
                保存済み素材の全commandを実行し、詳細結果を新規保存します。
              baseline-save --input <json> --output <json>
                完了したmanifestまたは実行結果をコピーし、指定baselineを置換します。
              compare-conversion --baseline <json> --current <json> --output <json>
                保存済み変換結果の再現性を比較し、差分を新規保存します。
              compare-run --baseline <json> --current <json> --output <json>
                保存済み実行結果の回帰を比較し、差分を新規保存します。
              verify --input <json>
                単一の保存済み実行結果だけで固定全入力の最終判定を行います。
              --help / <操作> --help
                操作説明を表示します。

            初回の操作例（各行を個別に実行）:
              generate --spec-root thirdParties/WebAssembly-spec --wabt-root thirdParties/wabt --wast2json {{converter}} --output artifacts/corpus
              run --manifest artifacts/corpus/manifest.json --output artifacts/run-1.json
              baseline-save --input artifacts/corpus/manifest.json --output artifacts/conversion-baseline.json
              baseline-save --input artifacts/run-1.json --output artifacts/run-baseline.json
            後続の操作例（比較結果を確認してからbaselineを明示更新）:
              run --manifest artifacts/corpus/manifest.json --output artifacts/run-2.json
              compare-run --baseline artifacts/run-baseline.json --current artifacts/run-2.json --output artifacts/run-diff.json
              baseline-save --input artifacts/run-2.json --output artifacts/run-baseline.json
              generate --spec-root thirdParties/WebAssembly-spec --wabt-root thirdParties/wabt --wast2json {{converter}} --output artifacts/corpus-2
              compare-conversion --baseline artifacts/conversion-baseline.json --current artifacts/corpus-2/manifest.json --output artifacts/conversion-diff.json
              baseline-save --input artifacts/corpus-2/manifest.json --output artifacts/conversion-baseline.json
              verify --input artifacts/run-2.json
            pathは起動時の作業ディレクトリを基準とします。
            終了値: 0=成立、1=用途の条件不成立、2=引数不正・読取/保存失敗・操作未完了
            """
        );
    }
}
