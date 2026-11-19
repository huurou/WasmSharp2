using System.ComponentModel;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Smoke;

namespace WasmSharp.Benchmarks;

/// <summary>
/// 素材確認・測定・保存結果比較の引数と終了値を扱う
/// </summary>
internal sealed class BenchmarkCli
{
    private readonly CaseJson _json = new CaseJson();
    private readonly MeasurementMethods _methods = new MeasurementMethods();
    private readonly WasmExecutionContext _context = new WasmExecutionContext();

    internal int Run(string[] args, TextWriter output, TextWriter error)
    {
        return Run(args, output, error, new BenchmarkRun(_json, _methods).Run);
    }

    /// <summary>
    /// 測定要求の実行を受け取り、引数の解釈と結果表示・終了値を処理する
    /// </summary>
    internal int Run(string[] args, TextWriter output, TextWriter error, Func<RunRequest, RunManifest> run)
    {
        try
        {
            if (args is (["--help"]) or (["check" or "run" or "compare", "--help"]))
            {
                WriteHelp(output);
                return 0;
            }
            return args.FirstOrDefault() switch
            {
                "check" => Check(ParseOptions(args, "--assets")["--assets"], output, error),
                "run" => Measure(ParseOptions(args, "--assets", "--case", "--output"), output, run),
                "compare" => Compare(ParseOptions(args, "--baseline", "--current", "--policy", "--output"), output),
                _ => throw new ArgumentException("check、run、compareのいずれかを指定してください。"),
            };
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
                        or JsonException
                        or InvalidOperationException
                        or Win32Exception
            )
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    private int Measure(Dictionary<string, string> options, TextWriter output, Func<RunRequest, RunManifest> run)
    {
        var directory = Path.GetFullPath(options["--output"]);
        EnsureNewOutput(directory);
        var kind = options.GetValueOrDefault("--kind", "measurement") switch
        {
            "measurement" => RunKind.Measurement,
            "smoke" => RunKind.Smoke,
            _ => throw new ArgumentException("用途はmeasurementまたはsmokeを指定してください。"),
        };
        var manifestPath = Path.GetFullPath(options["--assets"]);
        var assets = AssetManifest.Load(manifestPath, _json, _methods);
        var caseIds =
            options["--case"] == "all"
                ? [.. assets.Catalog.Cases.Select(x => x.CaseId).Order(StringComparer.Ordinal)]
                : new List<string> { assets.Catalog.Get(options["--case"]).CaseId };
        var command = CommandInvocation.FromProcess(
            Environment.ProcessPath!,
            Environment.GetCommandLineArgs(),
            Environment.CurrentDirectory
        );
        var repository = CaptureCommand.Run("git", command.WorkingDirectory, "rev-parse", "--show-toplevel").Trim();
        var result = run(
            new(
                directory,
                manifestPath,
                caseIds,
                kind,
                kind == RunKind.Smoke ? SmokeConfig.Create() : BenchmarkConfig.Create(),
                repository,
                command
            )
        );
        output.WriteLine($"run {result.RunId}: {result.Status}; 用途: {result.Kind}; 保存先: {directory}");
        foreach (var item in result.Cases)
        {
            output.WriteLine(
                $"{item.CaseId}: {item.Status}; ns/op={item.Metrics.Time?.MeanNanosecondsPerOperation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unavailable"}; bytes/op={item.Metrics.Allocation?.BytesPerOperation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unavailable"}"
            );
            foreach (var failure in item.Failures)
            {
                output.WriteLine($"  {failure.Stage}: {failure.Reason} {failure.Message}");
            }
        }
        return result.Status switch
        {
            ResultStatus.Succeeded => 0,
            ResultStatus.Failed or ResultStatus.Unavailable => 1,
            _ => 2,
        };
    }

    private int Check(string path, TextWriter output, TextWriter error)
    {
        var assets = AssetManifest.Load(path, _json, _methods);
        var succeeded = true;
        foreach (var definition in assets.Catalog.Cases.OrderBy(x => x.CaseId, StringComparer.Ordinal))
        {
            var result = CaseVerifier.Verify(_context, assets, definition.CaseId, _json);
            output.WriteLine($"{result.CaseId}: {(result.Succeeded ? "Succeeded" : "Failed")} ({result.Stage})");
            if (!result.Succeeded)
            {
                succeeded = false;
                error.WriteLine(
                    $"{result.CaseId}/{result.Stage}/{result.Subject}: 期待={result.Expected}, 観測={result.Observed}"
                );
            }
        }
        return succeeded ? 0 : 1;
    }

    private int Compare(Dictionary<string, string> options, TextWriter output)
    {
        var directory = Path.GetFullPath(options["--output"]);
        EnsureNewOutput(directory);
        var baseline = RunDirectories(options["--baseline"]);
        var current = RunDirectories(options["--current"]);
        foreach (var input in baseline.Concat(current))
        {
            if (
                directory.StartsWith(
                    Path.TrimEndingDirectorySeparator(input) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new IOException("比較入力run内へ出力できません。");
            }
        }
        var (report, hasIncompleteRun) = new RunComparison(_json).Compare(baseline, current, options["--policy"]);
        Directory.CreateDirectory(directory);
        using (var stream = new FileStream(Path.Combine(directory, "comparison.json"), FileMode.CreateNew))
        {
            JsonSerializer.Serialize(stream, report, _json.Options);
        }
        foreach (var result in report.Cases)
        {
            output.WriteLine($"{result.CaseId}: {result.Decision}");
            foreach (var reason in result.Reasons)
            {
                output.WriteLine($"  {reason.Kind}: {reason.Description}");
            }
        }
        output.WriteLine($"受入: {report.IsAccepted}; 保存先: {directory}");
        return hasIncompleteRun ? 2
            : report.IsAccepted ? 0
            : 1;
    }

    private static List<string> RunDirectories(string value)
    {
        var paths = value.Split(',');
        if (paths.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("run一覧は空要素を含まないカンマ区切りで指定してください。");
        }
        return [.. paths.Select(Path.GetFullPath)];
    }

    private static void EnsureNewOutput(string directory)
    {
        if (Path.Exists(directory))
        {
            throw new IOException("出力先は既に存在します。");
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args, params string[] required)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            if (!required.Contains(option) && !(args[0] == "run" && option == "--kind"))
            {
                throw new ArgumentException($"未知の引数です: {option}");
            }
            if (
                index + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[index + 1])
                || args[index + 1].StartsWith("--", StringComparison.Ordinal)
            )
            {
                throw new ArgumentException($"{option}の値が不足しています。");
            }
            if (!values.TryAdd(option, args[index + 1]))
            {
                throw new ArgumentException($"引数が重複しています: {option}");
            }
        }
        foreach (var option in required)
        {
            if (!values.ContainsKey(option))
            {
                throw new ArgumentException($"必須引数が不足しています: {option}");
            }
        }
        return values;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("check --assets <manifest>");
        output.WriteLine("run --assets <manifest> --case <id|all> --output <new-directory> [--kind measurement|smoke]");
        output.WriteLine(
            "compare --baseline <run-directory,...> --current <run-directory,...> --policy <path> --output <new-directory>"
        );
    }
}
