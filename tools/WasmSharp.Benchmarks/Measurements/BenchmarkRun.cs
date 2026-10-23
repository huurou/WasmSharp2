using System.Collections.Immutable;
using System.Security.Cryptography;
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
    /// 記録済みの設定でBDNを起動し、processの観測とrawを未完了runへ保存する
    /// 指標の抽出と成功の確定は、この実行境界の後に行う
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
            foreach (var caseId in request.CaseIds)
            {
                var reports = summaries
                    .SelectMany(x => x.Reports)
                    .Where(x =>
                        CaseCatalog.GetCaseId(
                            x.BenchmarkCase.Descriptor.Type.Name
                                + "."
                                + x.BenchmarkCase.Descriptor.WorkloadMethod.Name
                        ) == caseId
                    )
                    .ToList();
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
                                        y.IterationStage == IterationStage.Result
                                    ),
                                    null
                                ))
                        )
                    )
                    .ToList();
                var checkFailures = processes
                    .Select(x => LatestCheck(request.OutputDirectory, x.CheckFile))
                    .Where(x => x is not null)
                    .SelectMany(x => x!.Failures)
                    .ToList();
                var failures = processes
                    .Where(x => x.ExitCode != 0 || x.Errors.Count > 0)
                    .Select(x => new RunFailure(
                        caseId,
                        RunStage.BenchmarkProcessExit,
                        LatestCheck(request.OutputDirectory, x.CheckFile)?.LastConfirmedStage,
                        "benchmark processの終了または診断processの観測が未完了です。",
                        null,
                        string.Join(Environment.NewLine, x.Errors),
                        "終了値=" + x.ExitCode
                    ))
                    .Concat(checkFailures)
                    .ToList();
                prepared.Store.SaveCase(
                    new(
                        caseId,
                        checkFailures.Count > 0 ? ResultStatus.Failed : ResultStatus.Incomplete,
                        new(
                            null,
                            "指標の採取が完了していません。",
                            null,
                            "指標の採取が完了していません。",
                            null,
                            "指標の採取が完了していません。"
                        ),
                        new(
                            reports.Count > 0 && reports.All(x => x.BuildResult.IsBuildSuccess),
                            summaries.All(x => !x.HasCriticalValidationErrors),
                            processes
                        ),
                        failures,
                        raw
                    )
                );
            }
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
