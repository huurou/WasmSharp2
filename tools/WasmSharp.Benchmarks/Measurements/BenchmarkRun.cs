using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// runの実行前後に、比較条件と使用ファイルを記録する
/// </summary>
internal static class BenchmarkRun
{
    /// <summary>
    /// 予定を未完了runへ保存してから、Job別の子process設定と素材コピーを用意する
    /// </summary>
    internal static PreparedRun Prepare(RunRequest request)
    {
        var runId = Guid.NewGuid().ToString("N");
        var output = Path.GetFullPath(request.OutputDirectory);
        var store = RunStore.Create(
            output,
            new(
                1,
                runId,
                DateTimeOffset.UtcNow,
                null,
                request.Kind,
                ResultStatus.Incomplete,
                request.CaseIds,
                [],
                null,
                null,
                null,
                null,
                null,
                []
            )
        );
        try
        {
            var assets = AssetManifest.Load(request.AssetManifestPath);
            foreach (var caseId in request.CaseIds)
            {
                assets.Catalog.Get(caseId);
            }
            if (
                request.CaseIds.Count == 0
                || request.CaseIds.Distinct().Count() != request.CaseIds.Count
            )
            {
                throw new ArgumentException(
                    "予定ケースは重複のない1件以上で指定してください。",
                    nameof(request)
                );
            }
            var jobs = request
                .Config.GetJobs()
                .Select(job =>
                {
                    var configuration = new RunConfiguration(
                        runId,
                        output,
                        Path.Combine(output, "inputs", "assets", "asset-manifest.json"),
                        job.Id,
                        request.CaseIds.ToList()
                    );
                    var file = store.SaveFile(
                        "inputs/run-" + Guid.NewGuid().ToString("N") + ".json",
                        JsonSerializer.SerializeToUtf8Bytes(configuration, CaseJson.Options)
                    );
                    return job.WithEnvironmentVariable(
                        RunConfiguration.ENVIRONMENT_VARIABLE,
                        Path.Combine(output, file.RelativePath)
                    );
                })
                .ToImmutableArray();
            CaptureInputs(
                store,
                request.AssetManifestPath,
                jobs.Select(x => DescribeProcess(request.Kind.ToString(), x))
            );
            return new(store, runId, jobs);
        }
        catch (Exception exception)
        {
            store.SaveFailure(
                new(
                    null,
                    RunStage.Preparation,
                    null,
                    "runの準備に失敗しました。",
                    exception.GetType().FullName,
                    exception.Message,
                    null
                )
            );
            throw;
        }
    }

    /// <summary>
    /// 記録済みの設定でBDNを起動し、観測・検査・保存が揃った結果を確定する
    /// </summary>
    internal static RunManifest Run(RunRequest request)
    {
        var prepared = Prepare(request);
        var stage = RunStage.Preparation;
        try
        {
            CaptureEnvironment(
                prepared.Store,
                request.Command,
                prepared.Jobs.Select(x => DescribeProcess(request.Kind.ToString(), x))
            );
            var source = CaptureSourceBefore(
                request.RepositoryDirectory,
                prepared.Store,
                [
                    (AssemblyRole.Runtime, typeof(WasmModule).Assembly.Location),
                    (AssemblyRole.Harness, typeof(BenchmarkRun).Assembly.Location),
                ]
            );
            Summary[] summaries;
            try
            {
                var catalog = AssetManifest
                    .Load(
                        Path.Combine(
                            request.OutputDirectory,
                            "inputs",
                            "assets",
                            "asset-manifest.json"
                        )
                    )
                    .Catalog;
                var methods = request.CaseIds.Select(catalog.GetMeasurementMethod).ToHashSet();
                var config = BenchmarkDotNet
                    .Configs.ManualConfig.Create(request.Config)
                    .WithArtifactsPath(
                        Path.Combine(Path.GetFullPath(request.OutputDirectory), "raw", "bdn")
                    );
                var infos = new[] { typeof(InvokeBenchmarks), typeof(PreparationBenchmarks) }
                    .Select(type =>
                        (
                            Type: type,
                            Methods: type.GetMethods()
                                .Where(x => methods.Contains(type.Name + "." + x.Name))
                                .ToArray()
                        )
                    )
                    .Where(x => x.Methods.Length > 0)
                    .Select(x => BenchmarkConverter.MethodsToBenchmarks(x.Type, x.Methods, config))
                    .Select(info => new BenchmarkRunInfo(
                        info.BenchmarksCases.Select(x =>
                                BenchmarkDotNet.Running.BenchmarkCase.Create(
                                    x.Descriptor,
                                    prepared.Jobs.Single(job => job.Id == x.Job.Id),
                                    x.Parameters,
                                    x.Config
                                )
                            )
                            .ToArray(),
                        info.Type,
                        info.Config
                    ))
                    .ToArray();
                stage = RunStage.ExecutionStarted;
                summaries = BenchmarkRunner.Run(infos);
            }
            finally
            {
                CaptureSourceAfter(request.RepositoryDirectory, prepared.Store, source);
            }
            stage = RunStage.Saving;
            SaveReports(request, prepared, summaries);
        }
        catch (Exception exception)
        {
            prepared.Store.SaveFailure(
                new(
                    null,
                    stage,
                    null,
                    "runの実行が完了していません。",
                    exception.GetType().FullName,
                    exception.Message,
                    null
                )
            );
            throw;
        }
        return RunStore.Load(request.OutputDirectory);
    }

    /// <summary>
    /// BDNの観測を予定caseと対応付け、採用判定後に保存全体の完了を確認する
    /// </summary>
    internal static void SaveReports(
        RunRequest request,
        PreparedRun prepared,
        IEnumerable<Summary> summaries
    )
    {
        var summaryList = summaries.ToList();
        var rawDirectory = Path.Combine(request.OutputDirectory, "raw");
        var raw = Directory.Exists(rawDirectory)
            ? Directory
                .GetFiles(rawDirectory, "*", SearchOption.AllDirectories)
                .Select(x => new ManifestFile(
                    Path.GetRelativePath(request.OutputDirectory, x).Replace('\\', '/'),
                    Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x)))
                ))
                .ToList()
            : [];
        var results = new List<CaseRunResult>();
        foreach (var caseId in request.CaseIds)
        {
            var reports = summaryList
                .SelectMany(x => x.Reports)
                .Where(x =>
                    CaseCatalog.GetCaseId(
                        x.BenchmarkCase.Descriptor.Type.Name
                            + "."
                            + x.BenchmarkCase.Descriptor.WorkloadMethod.Name
                    ) == caseId
                )
                .ToList();
            var result = CaptureCase(request, prepared, summaryList, caseId, reports, raw);
            prepared.Store.SaveCase(result);
            results.Add(result);
        }
        if (results.All(x => x.Status != ResultStatus.Incomplete))
        {
            var status =
                results.Any(x => x.Status == ResultStatus.Failed) ? ResultStatus.Failed
                : results.Any(x => x.Status == ResultStatus.Unavailable) ? ResultStatus.Unavailable
                : ResultStatus.Succeeded;
            prepared.Store.Complete(status, results.SelectMany(x => x.Failures).ToList());
        }
    }

    private static CaseRunResult CaptureCase(
        RunRequest request,
        PreparedRun prepared,
        List<Summary> summaries,
        string caseId,
        List<BenchmarkReport> reports,
        List<ManifestFile> raw
    )
    {
        var processes = prepared
            .Jobs.SelectMany(job =>
                AttachChecks(
                    request.OutputDirectory,
                    prepared.RunId,
                    caseId,
                    job.Id,
                    reports
                        .Where(x => x.BenchmarkCase.Job.Id == job.Id)
                        .SelectMany(x => x.ExecuteResults)
                        .Select(x => new BenchmarkProcessResult(
                            job.Id,
                            x.ProcessId,
                            x.ExitCode,
                            x.Errors.ToList(),
                            x.Measurements.Any(y =>
                                y.IterationMode == IterationMode.Workload
                                && y.IterationStage == IterationStage.Result
                                && y.Operations > 0
                            ),
                            null
                        ))
                )
            )
            .ToList();
        var metrics = CaptureMetrics(prepared.Store, reports);
        var result = new CaseRunResult(
            caseId,
            ResultStatus.Incomplete,
            metrics.Metrics,
            new(
                reports.Count > 0 && reports.All(x => x.BuildResult.IsBuildSuccess),
                summaries.All(x => !x.HasCriticalValidationErrors),
                processes
            ),
            [],
            metrics.Measurements is null ? raw.ToList() : [.. raw, metrics.Measurements]
        );
        return EvaluateCase(request, prepared, summaries, reports, result);
    }

    private static CaseRunResult EvaluateCase(
        RunRequest request,
        PreparedRun prepared,
        List<Summary> summaries,
        List<BenchmarkReport> reports,
        CaseRunResult result
    )
    {
        var failures = new List<RunFailure>();
        var failed = false;
        var incomplete = false;
        void AddFailure(
            RunStage stage,
            string reason,
            RunStage? last = null,
            string? message = null,
            string? interruption = null
        ) => failures.Add(new(result.CaseId, stage, last, reason, null, message, interruption));

        if (
            reports.Count != prepared.Jobs.Length
            || !reports
                .Select(x => x.BenchmarkCase.Job.Id)
                .ToHashSet()
                .SetEquals(prepared.Jobs.Select(x => x.Id))
        )
        {
            incomplete = true;
            AddFailure(
                RunStage.BenchmarkProcessExit,
                "予定したcase/JobのBDN reportが揃っていません。"
            );
        }
        if (reports.Any(x => !x.BuildResult.IsBuildSuccess))
        {
            failed = true;
            AddFailure(
                RunStage.Build,
                "BDNのbuildが成功していません。",
                message: string.Join(
                    Environment.NewLine,
                    reports
                        .Where(x => !x.BuildResult.IsBuildSuccess)
                        .Select(x => x.BuildResult.ErrorMessage)
                )
            );
        }
        if (!result.BenchmarkReport!.ValidationSucceeded)
        {
            failed = true;
            AddFailure(
                RunStage.Validation,
                "BDNのvalidationに失敗しました。",
                message: string.Join(
                    Environment.NewLine,
                    summaries
                        .SelectMany(x => x.ValidationErrors)
                        .Where(x => x.IsCritical)
                        .Select(x => x.Message)
                )
            );
        }
        if (result.BenchmarkReport.Processes.Count == 0)
        {
            incomplete = true;
            AddFailure(
                RunStage.BenchmarkProcessExit,
                "予定したbenchmark processの実行記録がありません。"
            );
        }
        foreach (var process in result.BenchmarkReport.Processes)
        {
            var check = LatestCheck(request.OutputDirectory, process.CheckFile);
            if (check?.Failures.Count > 0)
            {
                failed = true;
                failures.AddRange(check.Failures);
            }
            if (process.Errors.Count > 0)
            {
                failed = true;
                AddFailure(
                    RunStage.BenchmarkProcessExit,
                    "BDNのErrorsに測定失敗が記録されています。",
                    check?.LastConfirmedStage,
                    string.Join(Environment.NewLine, process.Errors)
                );
            }
            foreach (
                var (stage, correctness) in new[]
                {
                    (RunStage.Precheck, check?.Precheck),
                    (RunStage.Postcheck, check?.Postcheck),
                }
            )
            {
                if (
                    correctness is not null
                    && (
                        correctness.Status == ResultStatus.Failed
                        || correctness.Observations.Any(x => !x.Matches)
                    )
                )
                {
                    failed = true;
                    if (!failures.Any(x => x.Stage == stage))
                    {
                        AddFailure(
                            stage,
                            "固定期待値との照合に失敗しました。",
                            check?.LastConfirmedStage
                        );
                    }
                }
            }
            if (
                process.ExitCode != 0
                || !process.HasResultMeasurements
                || check?.Precheck?.Status != ResultStatus.Succeeded
                || check.Postcheck?.Status != ResultStatus.Succeeded
                || !check.ExecutionStarted
            )
            {
                incomplete = true;
                AddFailure(
                    RunStage.BenchmarkProcessExit,
                    "同じprocessの正常終了・Result測定行・前後検査完了が揃っていません。",
                    check?.LastConfirmedStage,
                    interruption: "Job="
                        + process.JobId
                        + "; PID="
                        + process.ProcessId
                        + "; 終了値="
                        + (process.ExitCode?.ToString() ?? "取得不能")
                );
            }
        }
        var metrics = result.Metrics;
        var status =
            failed ? ResultStatus.Failed
            : incomplete ? ResultStatus.Incomplete
            : metrics.Time is null || metrics.Allocation is null || metrics.Gc is null
                ? ResultStatus.Unavailable
            : ResultStatus.Succeeded;
        return result with { Status = status, Failures = failures };
    }

    /// <summary>
    /// BDNの実行processと最新の検査snapshotを対応付け、追加診断processも保持する
    /// </summary>
    internal static List<BenchmarkProcessResult> AttachChecks(
        string outputDirectory,
        string runId,
        string caseId,
        string jobId,
        IEnumerable<BenchmarkProcessResult> processes
    )
    {
        var checks = RunStore
            .ReadChecks(outputDirectory)
            .Where(x =>
                x.Check.RunId == runId && x.Check.CaseId == caseId && x.Check.JobId == jobId
            )
            .GroupBy(x => x.Check.ProcessId)
            .Select(x =>
                x.OrderByDescending(y =>
                        y.Check.Postcheck is not null ? 3
                        : y.Check.ExecutionStarted ? 2
                        : y.Check.Precheck is not null ? 1
                        : 0
                    )
                    .ThenByDescending(y => y.Check.Failures.Count)
                    .First()
            )
            .ToDictionary(x => x.Check.ProcessId);
        var results = processes
            .Select(x =>
                x with
                {
                    CheckFile =
                        x.ProcessId is int pid && checks.TryGetValue(pid, out var check)
                            ? check.File
                            : null,
                }
            )
            .ToList();
        foreach (
            var check in checks.Values.Where(x =>
                !results.Any(y => y.ProcessId == x.Check.ProcessId)
            )
        )
        {
            results.Add(new(jobId, check.Check.ProcessId, null, [], false, check.File));
        }
        return results;
    }

    /// <summary>
    /// Workload/Resultの統計と別反復の割り当て・GCを対応付け、全測定行を保存する
    /// </summary>
    private static (CaseMetrics Metrics, ManifestFile? Measurements) CaptureMetrics(
        RunStore store,
        List<BenchmarkReport> reports
    )
    {
        if (reports.Count != 1)
        {
            const string REASON = "caseに対応する単一のBDN reportがありません。";
            return (new(null, REASON, null, REASON, null, REASON), null);
        }

        var report = reports[0];
        var csv = new StringBuilder(
            "launch_index,iteration_index,iteration_mode,iteration_stage,operations,elapsed_nanoseconds\n"
        );
        foreach (var measurement in report.AllMeasurements)
        {
            csv.AppendLine(
                FormattableString.Invariant(
                    $"{measurement.LaunchIndex},{measurement.IterationIndex},{measurement.IterationMode},{measurement.IterationStage},{measurement.Operations},{measurement.Nanoseconds:R}"
                )
            );
        }
        var measurements = store.SaveFile(
            "raw/metrics/" + Guid.NewGuid().ToString("N") + "-measurements.csv",
            Encoding.UTF8.GetBytes(csv.ToString())
        );
        var statistics = report.ResultStatistics;
        TimeStatistics? time = null;
        if (
            statistics is not null
            && double.IsFinite(statistics.ConfidenceInterval.Lower)
            && double.IsFinite(statistics.ConfidenceInterval.Upper)
        )
        {
            time = new(
                statistics.N,
                statistics.Mean,
                statistics.Median,
                statistics.StandardDeviation,
                new(
                    statistics.ConfidenceInterval.Lower,
                    statistics.ConfidenceInterval.Upper,
                    0.999
                ),
                measurements
            );
        }
        var gcStats = report.GcStats;
        var totalBytes = gcStats.GetTotalAllocatedBytes(false);
        AllocationStatistics? allocation = null;
        GcStatistics? gc = null;
        if (gcStats.TotalOperations > 0)
        {
            var displayedBytes = gcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase);
            if (totalBytes is long bytes && displayedBytes is long displayed)
            {
                allocation = new(
                    bytes,
                    gcStats.TotalOperations,
                    (double)bytes / gcStats.TotalOperations,
                    displayed,
                    new(
                        0,
                        MidpointRounding.ToEven,
                        "BenchmarkDotNet0.15.8は総bytes/操作数を整数へToEven丸めする。表示0は割り当て不存在を意味しない。"
                    ),
                    AllocationScope.ProcessManagedAllocations,
                    ObservationIteration.SeparateFromTiming
                );
            }
            gc = new(
                gcStats.TotalOperations,
                new(
                    gcStats.Gen0Collections,
                    gcStats.Gen0Collections / (double)gcStats.TotalOperations * 1000
                ),
                new(
                    gcStats.Gen1Collections,
                    gcStats.Gen1Collections / (double)gcStats.TotalOperations * 1000
                ),
                new(
                    gcStats.Gen2Collections,
                    gcStats.Gen2Collections / (double)gcStats.TotalOperations * 1000
                ),
                ObservationIteration.SeparateFromTiming
            );
        }
        return (
            new(
                time,
                time is null ? "BDNの時間統計または99.9%信頼区間を取得できませんでした。" : null,
                allocation,
                allocation is null
                    ? "BDNのmanaged割り当て総量またはGC観測操作数を取得できませんでした。"
                    : null,
                gc,
                gc is null ? "BDNのGC観測操作数を取得できませんでした。" : null
            ),
            measurements
        );
    }

    private static ProcessCheck? LatestCheck(string outputDirectory, ManifestFile? file) =>
        file is null
            ? null
            : JsonSerializer.Deserialize<ProcessCheck>(
                File.ReadAllBytes(Path.Combine(outputDirectory, file.RelativePath)),
                CaseJson.Options
            );

    /// <summary>
    /// Jobの明示値とBDNへ委ねる既定値を区別してprocess構成を記録する
    /// </summary>
    internal static ProcessConfiguration DescribeProcess(string role, Job job)
    {
        return EnvironmentCapture.DescribeProcess(role, job);
    }

    /// <summary>
    /// 採用済み素材の内容を照合して、ケース定義・Job設定とともにrunへ保存する
    /// </summary>
    internal static void CaptureInputs(
        RunStore store,
        string assetManifestPath,
        IEnumerable<ProcessConfiguration> processes
    )
    {
        var loaded = AssetManifest.Load(assetManifestPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(assetManifestPath))!;
        var files = new[] { loaded.Manifest.Profile, loaded.Manifest.Cases }
            .Concat(loaded.Manifest.Assets.Select(x => x.Binary))
            .Concat(
                loaded
                    .Manifest.Assets.SelectMany(x => x.Sources)
                    .Select(x => new ManifestFile(x.RelativePath, x.Sha256))
            )
            .DistinctBy(x => x.RelativePath);
        ManifestFile? cases = null;
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, file.RelativePath));
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
            {
                throw new InvalidDataException(
                    "コピーする素材のSHA-256が一致しません: " + file.RelativePath
                );
            }

            var saved = store.SaveFile("inputs/assets/" + file.RelativePath, bytes);
            if (file.RelativePath == loaded.Manifest.Cases.RelativePath)
            {
                cases = saved;
            }
        }

        var manifest = store.SaveFile(
            "inputs/assets/asset-manifest.json",
            File.ReadAllBytes(assetManifestPath)
        );
        var job = store.SaveFile(
            "inputs/job.json",
            JsonSerializer.SerializeToUtf8Bytes(
                processes.Select(EnvironmentCapture.SnapshotProcess).ToList(),
                CaseJson.Options
            )
        );
        store.SaveInputs(manifest, cases!, job);
    }

    /// <summary>
    /// 採取processの環境と指定された測定・診断構成を同じrunへ保存する
    /// </summary>
    internal static RunEnvironment CaptureEnvironment(
        RunStore store,
        CommandInvocation command,
        IEnumerable<ProcessConfiguration> processes
    )
    {
        var environment = EnvironmentCapture.Capture(store, command, processes);
        store.SaveEnvironment(environment);
        return environment;
    }

    /// <summary>
    /// 使用source・assemblyと復元用の差分・未追跡ファイルを実行前に保存する
    /// </summary>
    internal static RunSource CaptureSourceBefore(
        string repositoryDirectory,
        RunStore store,
        IEnumerable<(AssemblyRole Role, string Path)> assemblies
    )
    {
        var source = new SourceCapture(repositoryDirectory).Before(store, assemblies);
        store.SaveSource(source);
        return source;
    }

    /// <summary>
    /// 実行前に使用したファイルと新しい使用sourceの内容変化を照合して追記する
    /// </summary>
    internal static RunSource CaptureSourceAfter(
        string repositoryDirectory,
        RunStore store,
        RunSource before
    )
    {
        var source = new SourceCapture(repositoryDirectory).After(before);
        store.SaveSource(source);
        return source;
    }
}
