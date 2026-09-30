using System.Runtime.InteropServices;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 保存済みのmanifestと照合済み素材から、入力ごとに状態を分離して全commandを実行する
/// </summary>
internal static class SuiteExecutor
{
    /// <summary>
    /// 全入力の結果と素材・実行条件のスナップショットを作る。
    /// </summary>
    /// <param name="manifest">全対象入力と変換済み素材の記録</param>
    /// <param name="manifestPath">相対配置の基準となるmanifestのpath</param>
    /// <param name="cancellationToken">command間で通知を確認する中断要求。実行中のWasmを強制停止しない</param>
    internal static RunReport Execute(
        CorpusManifest manifest,
        string manifestPath,
        CancellationToken cancellationToken = default
    )
    {
        var absolutePath = Path.GetFullPath(manifestPath);
        var report = RunReport.Create(
            manifest,
            new(Guid.NewGuid().ToString("D"))
            {
                StartedAt = DateTimeOffset.UtcNow,
                RunnerVersion = typeof(SuiteExecutor).Assembly.GetName().Version?.ToString(),
                RuntimeVersion = typeof(WasmModule).Assembly.GetName().Version?.ToString(),
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                ManifestPath = absolutePath,
            },
            new(ScriptExecutor.MAX_CALL_DEPTH)
        );
        try
        {
            var verification = CorpusVerifier.Verify(
                report.Corpus,
                absolutePath,
                VerificationMode.Execution,
                sourceRoot: null
            );
            report.Diagnostics.AddRange(
                report.Corpus.Diagnostics.Concat(verification.Diagnostics).Distinct()
            );
            var inputs = verification.Inputs.ToDictionary(
                x => x.Input.Path,
                StringComparer.Ordinal
            );
            var conversions = report.Corpus.Inputs.ToDictionary(
                x => x.Input.Path,
                StringComparer.Ordinal
            );
            for (var i = 0; i < report.Inputs.Count; i++)
            {
                var path = report.Inputs[i].InputPath;
                cancellationToken.ThrowIfCancellationRequested();
                if (!inputs.TryGetValue(path, out var input))
                {
                    report.Inputs[i] = new(path)
                    {
                        Status = InputRunStatus.Processed,
                        Issues = [new("verify", "manifestに対象入力の記録がありません。", path)],
                    };
                    continue;
                }
                input = input with
                {
                    Issues = [.. conversions[path].Diagnostics.Concat(input.Issues).Distinct()],
                };
                var state = new ScriptState(path);
                state.Register(SpectestFactory.Create(state));
                var (result, interruption) = ExecuteInput(input, state, cancellationToken);
                report.Inputs[i] = result;
                if (interruption is not null)
                {
                    report.Diagnostics.Add(interruption);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            report.Diagnostics.Add(
                new("execute_suite", exception.Message, null, exception.GetType().FullName)
            );
        }
        return report with
        {
            Summary = report.Summarize(),
            Completion = new(report.Inputs.All(x => x.Status == InputRunStatus.Processed), false),
        };
    }

    /// <summary>
    /// 全入力を実行し、部分結果を含め保存可能な記録を確定保存する。
    /// </summary>
    /// <param name="manifest">全対象入力と素材の記録</param>
    /// <param name="manifestPath">素材の相対配置の基準</param>
    /// <param name="outputPath">既存ファイルを上書きしない詳細結果の保存先</param>
    /// <param name="cancellationToken">command間で確認する中断要求</param>
    internal static RunResult ExecuteAndSave(
        CorpusManifest manifest,
        string manifestPath,
        string outputPath,
        CancellationToken cancellationToken = default
    )
    {
        var absoluteOutput = Path.GetFullPath(outputPath);
        var report = Execute(manifest, manifestPath, cancellationToken);
        var stored = report with { Completion = report.Completion with { OutputComplete = true } };
        try
        {
            ReportStore.Save(stored, absoluteOutput);
            return new(stored, absoluteOutput, null);
        }
        catch (ReportStoreException exception)
        {
            return new(report, absoluteOutput, exception);
        }
    }

    /// <summary>
    /// 一つの入力を処理し、検知できた中断では処理済みケースと未処理の末尾を保持する。
    /// </summary>
    /// <param name="input">照合済みdocumentとbinary、入力異常</param>
    /// <param name="state">この入力のために初期化した状態</param>
    /// <param name="cancellationToken">command間で確認する中断要求</param>
    /// <returns>入力の記録と、途中中断した場合の操作診断</returns>
    internal static (InputRunResult Result, CorpusDiagnostic? Interruption) ExecuteInput(
        InputVerification input,
        ScriptState state,
        CancellationToken cancellationToken
    )
    {
        var result = new InputRunResult(input.Input.Path)
        {
            Status = InputRunStatus.Incomplete,
            CommandCount = input.Document?.CommandCount,
            EnumeratedCount = input.Document?.Commands.Length ?? 0,
            Issues = [.. input.Issues],
        };
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.Document is { } document)
            {
                var script = ScriptReader.Read(document, input.Input.Path);
                var executor = new ScriptExecutor(input);
                foreach (var command in script.Commands)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Cases.Add(executor.Execute(command, state));
                }
            }
            return (result with { Status = InputRunStatus.Processed }, null);
        }
        catch (Exception exception)
        {
            return (
                result with
                {
                    UnprocessedCount = result.EnumeratedCount - result.Cases.Count,
                },
                new(
                    "execute_input",
                    exception.Message,
                    input.Input.Path,
                    exception.GetType().FullName
                )
            );
        }
    }
}
