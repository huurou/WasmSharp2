using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 計時外の設定読込とprocess別の検査snapshotを所有する
/// </summary>
internal sealed class BenchmarkProcess
{
    private readonly RunConfiguration configuration_;
    private readonly RunStore store_;
    private ProcessCheck check_;

    private BenchmarkProcess(RunConfiguration configuration, string caseId)
    {
        configuration_ = configuration;
        store_ = RunStore.OpenChecks(configuration.OutputDirectory, configuration.RunId);
        check_ = new(
            configuration.RunId,
            caseId,
            configuration.JobId,
            Environment.ProcessId,
            null,
            false,
            null,
            null,
            [],
            [],
            RuntimeVersion: EnvironmentCapture.CurrentRuntimeVersion
        );
    }

    internal static BenchmarkProcess Start(string measurementMethod)
    {
        var path =
            Environment.GetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE)
            ?? throw new InvalidOperationException("run設定の環境変数がありません。");
        var configuration = JsonSerializer.Deserialize<RunConfiguration>(
            File.ReadAllBytes(path),
            CaseJson.Options
        )!;
        var caseId = CaseCatalog.GetCaseId(measurementMethod);
        if (!configuration.PlannedCaseIds.Contains(caseId))
        {
            throw new InvalidDataException("測定メソッドがrunの予定ケースにありません。");
        }
        return new(configuration, caseId);
    }

    internal void Setup(Action<LoadedAssets, string> initialize)
    {
        RunChecked(
            RunStage.Precheck,
            () =>
            {
                CaptureAffinity(after: false);
                check_ = check_ with
                {
                    LoadedAssemblies = [.. new[]
                    {
                        (AssemblyRole.Runtime, typeof(WasmModule).Assembly.Location),
                        (AssemblyRole.Harness, typeof(InvokeBenchmarks).Assembly.Location),
                    }
                        .Select(x => new AssemblyIdentity(
                            x.Item1,
                            x.Location,
                            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x.Location)))
                        ))],
                };
            }
        );
        Save();
        RunChecked(
            RunStage.Precheck,
            () => initialize(AssetManifest.Load(configuration_.AssetManifestPath), check_.CaseId)
        );
        check_ = check_ with { Precheck = Success(), LastConfirmedStage = RunStage.Precheck };
        Save();
        Save(
            check_ with
            {
                ExecutionStarted = true,
                LastConfirmedStage = RunStage.ExecutionStarted,
            }
        );
    }

    internal void Cleanup(Action verify)
    {
        RunChecked(
            RunStage.Postcheck,
            () =>
            {
                CaptureAffinity(after: true);
                verify();
            }
        );
        check_ = check_ with { Postcheck = Success(), LastConfirmedStage = RunStage.Postcheck };
        Save();
    }

    private void RunChecked(RunStage stage, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Fail(stage, exception);
            throw;
        }
    }

    private static CorrectnessCheck Success()
    {
        return new(ResultStatus.Succeeded, [new("固定期待値との照合", "全項目一致", "全項目一致", true)]);
    }

    private void CaptureAffinity(bool after)
    {
        long? mask = null;
        string? reason = null;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                mask = process.ProcessorAffinity.ToInt64();
                if (mask == 0)
                {
                    mask = null;
                    reason = "processのaffinityが0で観測できません。";
                }
            }
            catch (Exception exception)
                when (exception
                        is Win32Exception
                            or InvalidOperationException
                            or PlatformNotSupportedException
                )
            {
                reason = exception.GetType().Name + ": " + exception.Message;
            }
        }
        else
        {
            reason = "processのaffinity観測はWindows/Linuxだけに対応しています。";
        }

        check_ = check_ with
        {
            AffinityBefore = after ? check_.AffinityBefore : mask,
            AffinityAfter = after ? mask : check_.AffinityAfter,
            AffinityUnavailableReason = reason is null
                ? check_.AffinityUnavailableReason
                : (after ? "後観測: " : "前観測: ") + reason,
        };
    }

    private void Fail(RunStage stage, Exception exception)
    {
        var failed = new CorrectnessCheck(
            ResultStatus.Failed,
            [new("固定期待値との照合", "全項目一致", exception.Message, false)]
        );
        check_ = check_ with
        {
            Precheck = stage == RunStage.Precheck ? failed : check_.Precheck,
            Postcheck = stage == RunStage.Postcheck ? failed : check_.Postcheck,
        };
        AddFailure(stage, "計時外の準備または検査に失敗しました。", exception);
        try
        {
            Save();
        }
        catch
        {
            // 保存の失敗はFailuresへ保持し、元の検査例外を呼出元へ返す。
        }
    }

    private void AddFailure(RunStage stage, string reason, Exception exception)
    {
        check_ = check_ with
        {
            Failures =
            [
                .. check_.Failures,
                new(
                    check_.CaseId,
                    stage,
                    check_.LastConfirmedStage,
                    reason,
                    exception.GetType().FullName,
                    exception.Message,
                    null
                ),
            ],
        };
    }

    private void Save(ProcessCheck? snapshot = null)
    {
        var saved = snapshot ?? check_;
        try
        {
            store_.SaveCheck(saved);
        }
        catch (Exception exception)
        {
            AddFailure(RunStage.Saving, "検査snapshotの保存に失敗しました。", exception);
            throw;
        }
        check_ = saved;
    }
}
