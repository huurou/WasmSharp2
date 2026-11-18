using System.Text.Json;
using BenchmarkDotNet.Running;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_RunTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();
    private readonly BenchmarkRun benchmarkRun_;

    public BenchmarkRun_RunTests()
    {
        benchmarkRun_ = new BenchmarkRun(json_, methods_);
    }

    [Test]
    [Arguments(true, ResultStatus.Succeeded)]
    [Arguments(false, ResultStatus.Failed)]
    public async Task 測定reportを受け取る_保存済み条件で予定メソッドだけを実行し成否を確定する(
        bool buildSucceeded,
        ResultStatus expectedStatus
    )
    {
        // Arrange
        using var repository = new SourceCaptureFixture();
        using var assets = new AssetLoadingFixture();
        using var report = new BenchmarkReportFixture(buildSucceeded: buildSucceeded);
        var request = report.Request with
        {
            OutputDirectory = repository.Run.DirectoryPath,
            AssetManifestPath = assets.ManifestPath,
            RepositoryDirectory = repository.RepositoryDirectory,
            Command = new CommandInvocation("dotnet", ["run"], repository.RepositoryDirectory),
        };
        BenchmarkRunInfo[]? captured = null;
        RunManifest? before = null;
        RunConfiguration? configuration = null;
        var calls = 0;

        // Act
        var actual = benchmarkRun_.Run(
            request,
            infos =>
            {
                captured = infos;
                calls++;
                before = RunStore.Load(request.OutputDirectory, json_);
                var benchmark = infos.Single().BenchmarksCases.Single();
                var path = benchmark
                    .Job.Environment.EnvironmentVariables.Single(x => x.Key == RunConfiguration.ENVIRONMENT_VARIABLE)
                    .Value;
                configuration = JsonSerializer.Deserialize<RunConfiguration>(File.ReadAllBytes(path), json_.Options)!;
                var source = ReadSource(request.OutputDirectory, before);
                RunStore
                    .OpenChecks(request.OutputDirectory, before.RunId, json_)
                    .SaveCheck(
                        report.SuccessfulCheck() with
                        {
                            RunId = before.RunId,
                            LoadedAssemblies = source.AssembliesBefore,
                            RuntimeVersion = EnvironmentCapture.CurrentRuntimeVersion,
                        }
                    );
                var rawDirectory = Path.Combine(request.OutputDirectory, "raw", "bdn");
                Directory.CreateDirectory(rawDirectory);
                File.WriteAllText(Path.Combine(rawDirectory, "result.txt"), "BDNの保存出力");
                return [report.Summary];
            }
        );
        var sourceAfter = ReadSource(request.OutputDirectory, actual);
        var benchmarkInfo = captured!.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(before!.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(before.EnvironmentFile).IsNotNull();
            await Assert.That(before.SourceFile).IsNotNull();
            await Assert.That(benchmarkInfo.Type).IsEqualTo(typeof(InvokeBenchmarks));
            await Assert
                .That(benchmarkInfo.BenchmarksCases.Single().Descriptor.WorkloadMethod.Name)
                .IsEqualTo(nameof(InvokeBenchmarks.Single));
            await Assert
                .That(benchmarkInfo.Config.ArtifactsPath)
                .IsEqualTo(Path.Combine(request.OutputDirectory, "raw", "bdn"));
            await Assert.That(configuration!.RunId).IsEqualTo(actual.RunId);
            await Assert.That(configuration.JobId).IsEqualTo("Measurement");
            await Assert.That(configuration.PlannedCaseIds).IsEquivalentTo(["invoke.single"]);
            await Assert
                .That(configuration.AssetManifestPath)
                .IsEqualTo(Path.Combine(request.OutputDirectory, "inputs", "assets", "asset-manifest.json"));
            await Assert.That(actual.Status).IsEqualTo(expectedStatus);
            await Assert.That(actual.CompletedAtUtc).IsNotNull();
            await Assert.That(actual.Cases.Single().Status).IsEqualTo(expectedStatus);
            await Assert.That(actual.Cases.Single().Metrics.Time).IsNotNull();
            await Assert.That(actual.Cases.Single().RawFiles.Select(x => x.RelativePath)).Contains("raw/bdn/result.txt");
            await Assert.That(sourceAfter.AssembliesAfter).IsEquivalentTo(sourceAfter.AssembliesBefore);
            await Assert.That(sourceAfter.Changes).IsEmpty();
            await Assert
                .That(Directory.GetFiles(Path.Combine(request.OutputDirectory, "source"), "source-*.json").Length)
                .IsEqualTo(2);
            if (!buildSucceeded)
            {
                await Assert.That(actual.Failures.Any(x => x.Stage == RunStage.Build)).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(false, RunStage.ExecutionStarted)]
    [Arguments(true, RunStage.Saving)]
    public async Task 実行または保存が失敗する_sourceの事後変化と失敗段階を未完了runへ残す(
        bool savingFails,
        RunStage expectedStage
    )
    {
        // Arrange
        using var repository = new SourceCaptureFixture();
        using var assets = new AssetLoadingFixture();
        using var report = new BenchmarkReportFixture();
        var request = report.Request with
        {
            OutputDirectory = repository.Run.DirectoryPath,
            AssetManifestPath = assets.ManifestPath,
            RepositoryDirectory = repository.RepositoryDirectory,
            Command = new CommandInvocation("dotnet", ["run"], repository.RepositoryDirectory),
        };
        var expected = new InvalidOperationException("BDN実行失敗");

        // Act & Assert
        var exception = await Assert
            .That(() =>
                benchmarkRun_.Run(
                    request,
                    _ =>
                    {
                        repository.Write("src/WasmSharp/Runtime.cs", "class ChangedRuntime {}\n");
                        if (!savingFails)
                        {
                            throw expected;
                        }
                        Directory.CreateDirectory(Path.Combine(request.OutputDirectory, "raw"));
                        File.WriteAllText(Path.Combine(request.OutputDirectory, "raw", "metrics"), "保存先を塞ぐファイル");
                        return [report.Summary];
                    }
                )
            )
            .Throws<Exception>();
        var actual = RunStore.Load(request.OutputDirectory, json_);
        var source = ReadSource(request.OutputDirectory, actual);
        var failure = actual.Failures.Single(x => x.ExceptionType is not null);
        using (Assert.Multiple())
        {
            await Assert.That(actual.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(actual.CompletedAtUtc).IsNull();
            await Assert.That(failure.Stage).IsEqualTo(expectedStage);
            await Assert.That(failure.ExceptionType).IsEqualTo(exception!.GetType().FullName);
            await Assert.That(failure.Message).IsEqualTo(exception.Message);
            await Assert.That(failure.Reason).Contains("runの実行が完了していません");
            await Assert.That(source.Changes.Single().Path).IsEqualTo("src/WasmSharp/Runtime.cs");
            await Assert.That(source.AssembliesAfter).IsEquivalentTo(source.AssembliesBefore);
            if (!savingFails)
            {
                await Assert.That(exception).IsSameReferenceAs(expected);
            }
            else
            {
                await Assert.That(exception).IsTypeOf<IOException>();
            }
        }
    }

    private RunSource ReadSource(string directory, RunManifest manifest)
    {
        return JsonSerializer.Deserialize<RunSource>(
            File.ReadAllBytes(Path.Combine(directory, manifest.SourceFile!.RelativePath)),
            json_.Options
        )!;
    }
}
