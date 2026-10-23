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
            []
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
        try
        {
            check_ = check_ with
            {
                LoadedAssemblies = new[]
                {
                    (AssemblyRole.Runtime, typeof(WasmModule).Assembly.Location),
                    (AssemblyRole.Harness, typeof(InvokeBenchmarks).Assembly.Location),
                }
                    .Select(x => new AssemblyIdentity(
                        x.Item1,
                        x.Location,
                        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x.Location)))
                    ))
                    .ToList(),
            };
            store_.SaveCheck(check_);
            initialize(AssetManifest.Load(configuration_.AssetManifestPath), check_.CaseId);
            check_ = check_ with { Precheck = Success(), LastConfirmedStage = RunStage.Precheck };
            Save();
            check_ = check_ with
            {
                ExecutionStarted = true,
                LastConfirmedStage = RunStage.ExecutionStarted,
            };
            Save();
        }
        catch (Exception exception)
        {
            Fail(RunStage.Precheck, exception);
            throw;
        }
    }

    internal void Cleanup(Action verify)
    {
        try
        {
            verify();
            check_ = check_ with { Postcheck = Success(), LastConfirmedStage = RunStage.Postcheck };
            Save();
        }
        catch (Exception exception)
        {
            Fail(RunStage.Postcheck, exception);
            throw;
        }
    }

    private static CorrectnessCheck Success() =>
        new(ResultStatus.Succeeded, [new("固定期待値との照合", "全項目一致", "全項目一致", true)]);

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
            Failures =
            [
                .. check_.Failures,
                new(
                    check_.CaseId,
                    stage,
                    check_.LastConfirmedStage,
                    "計時外の準備または検査に失敗しました。",
                    exception.GetType().FullName,
                    exception.Message,
                    null
                ),
            ],
        };
        Save();
    }

    private void Save() => store_.SaveCheck(check_);
}
