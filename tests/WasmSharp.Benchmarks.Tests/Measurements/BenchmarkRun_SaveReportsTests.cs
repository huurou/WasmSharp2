using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Toolchains.Results;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_SaveReportsTests
{
    [Test]
    [Arguments(83)]
    [Arguments(null)]
    public async Task reportと測定行があっても子processが正常終了しない_最後の確認段階を残して未完了になる(
        int? exitCode
    )
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(exitCode: exitCode);
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(
            fixture.SuccessfulCheck() with
            {
                Postcheck = null,
                LastConfirmedStage = RunStage.ExecutionStarted,
            }
        );

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);
        var result = manifest.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.CompletedAtUtc).IsNull();
            await Assert
                .That(result.BenchmarkReport!.Processes.Single().ExitCode)
                .IsEqualTo(exitCode);
            await Assert
                .That(result.Failures.All(x => x.LastConfirmedStage == RunStage.ExecutionStarted))
                .IsTrue();
            await Assert
                .That(
                    result.Failures.Any(x =>
                        x.Stage == RunStage.BenchmarkProcessExit
                        && x.InterruptionReason!.Contains("PID=123")
                    )
                )
                .IsTrue();
            await Assert.That(result.RawFiles.Count).IsEqualTo(2);
        }
    }

    [Test]
    public async Task 先頭processが成功しても後続processが非0終了する_全processを突合して未完了になる()
    {
        // Arrange
        var lines = BenchmarkReportFixture.ReadLines();
        var extra = new ExecuteResult(
            true,
            83,
            124,
            lines.Where(x => !x.StartsWith("//")).ToArray(),
            lines.Where(x => x.StartsWith("//")).ToArray(),
            lines,
            2
        );
        using var fixture = new BenchmarkReportFixture(additionalProcesses: [extra]);
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck(124));

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(result.BenchmarkReport!.Processes).Count().IsEqualTo(2);
            await Assert
                .That(result.Failures.Any(x => x.InterruptionReason!.Contains("PID=124")))
                .IsTrue();
        }
    }

    [Test]
    public async Task 子process終了0でもBDNのErrorsがある_原因とrawを残して失敗になる()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            BenchmarkReportFixture.ReadLines().Append("// ERROR: Exception during GlobalCleanup!")
        );
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);
        var result = manifest.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Failed);
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Failed);
            await Assert
                .That(result.BenchmarkReport!.Processes.Single().Errors)
                .Contains("Exception during GlobalCleanup!");
            await Assert
                .That(result.Failures.Any(x => x.Message!.Contains("GlobalCleanup")))
                .IsTrue();
            await Assert.That(result.RawFiles.Count).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 前後検査のどちらかが不一致である_成功したreportでも失敗になる(bool postcheck)
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();
        fixture.SaveConditions();
        var check = fixture.SuccessfulCheck();
        var failed = new CorrectnessCheck(ResultStatus.Failed, [new("result", "1", "2", false)]);
        fixture.Prepared.Store.SaveCheck(
            check with
            {
                Precheck = postcheck ? check.Precheck : failed,
                Postcheck = postcheck ? failed : check.Postcheck,
            }
        );

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Failed);
            await Assert.That(manifest.Cases.Single().Status).IsEqualTo(ResultStatus.Failed);
            await Assert
                .That(
                    manifest
                        .Cases.Single()
                        .Failures.Any(x =>
                            x.Stage == (postcheck ? RunStage.Postcheck : RunStage.Precheck)
                        )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task process検査にtrapや例外が記録される_例外型とmessageを保って失敗になる()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();
        fixture.SaveConditions();
        var failure = new RunFailure(
            "invoke.single",
            RunStage.Postcheck,
            RunStage.ExecutionStarted,
            "固定期待値の検査中にtrap",
            "WasmSharp.WasmTrapException",
            "trapの詳細",
            null
        );
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck() with { Failures = [failure] });

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Failed);
            await Assert.That(manifest.Cases.Single().Failures).Contains(failure);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task buildかvalidationが不成立である_発生段階を保存して失敗になる(bool validation)
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            buildSucceeded: validation,
            validationSucceeded: !validation
        );
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Failed);
            await Assert
                .That(
                    manifest
                        .Cases.Single()
                        .Failures.Any(x =>
                            x.Stage == (validation ? RunStage.Validation : RunStage.Build)
                        )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 必須GCが欠落して前後検査と終了は成立する_取得不能と理由を保存する()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            BenchmarkReportFixture.ReadLines().Where(x => !x.StartsWith("// GC:"))
        );
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);
        var result = manifest.Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Unavailable);
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Unavailable);
            await Assert.That(result.Metrics.Time).IsNotNull();
            await Assert.That(result.Metrics.Allocation).IsNull();
            await Assert
                .That(result.Metrics.AllocationUnavailableReason)
                .Contains("取得できません");
            await Assert.That(result.Metrics.Gc).IsNull();
            await Assert.That(result.Metrics.GcUnavailableReason).Contains("取得できません");
        }
    }

    [Test]
    public async Task Result1行で時間の信頼区間を取得できない_前後検査が成功しても取得不能になる()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture([
            "WorkloadResult 1: 64 op, 43100.00 ns",
            "// GC: 0 0 0 24576 64",
        ]);
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Unavailable);
            await Assert.That(manifest.Cases.Single().Metrics.Time).IsNull();
            await Assert
                .That(manifest.Cases.Single().Metrics.TimeUnavailableReason)
                .Contains("信頼区間");
        }
    }

    [Test]
    public async Task 予定したcaseのreportがない_欠落理由と未完了を保存して他caseを補完しない()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            plannedCaseIds: ["invoke.single", "decode.small"]
        );
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);
        var missing = manifest.Cases.Single(x => x.CaseId == "decode.small");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert
                .That(manifest.Cases.Single(x => x.CaseId == "invoke.single").Status)
                .IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(missing.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert
                .That(missing.Failures.Any(x => x.Reason.Contains("reportが揃っていません")))
                .IsTrue();
            await Assert.That(missing.Metrics.Time).IsNull();
            await Assert.That(missing.Metrics.TimeUnavailableReason).Contains("report");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Result測定行か実行processがない_前後検査があっても未完了になる(
        bool notExecuted
    )
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            BenchmarkReportFixture.ReadLines().Where(x => !x.StartsWith("WorkloadResult")),
            executed: !notExecuted
        );
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(result.Failures.Any(x => x.Reason.Contains("Result測定行"))).IsTrue();
        }
    }

    [Test]
    [Arguments("run")]
    [Arguments("case")]
    [Arguments("job")]
    [Arguments("pid")]
    public async Task 成功検査の識別情報が対応しない_別processの検査で成功へ補完しない(
        string difference
    )
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();
        fixture.SaveConditions();
        var check = fixture.SuccessfulCheck();
        check = difference switch
        {
            "run" => check with { RunId = "another-run" },
            "case" => check with { CaseId = "decode.small" },
            "job" => check with { JobId = "OtherJob" },
            _ => check with { ProcessId = 124 },
        };
        fixture.Prepared.Store.SaveFile(
            "checks/foreign.json",
            JsonSerializer.SerializeToUtf8Bytes(check, CaseJson.Options)
        );

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert
                .That(result.BenchmarkReport!.Processes.Single(x => x.ProcessId == 123).CheckFile)
                .IsNull();
            await Assert.That(result.Failures.Any(x => x.Reason.Contains("前後検査完了"))).IsTrue();
        }
    }

    [Test]
    public async Task 追加診断processの前後検査だけある_終了値を推測せず未完了になる()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck(124));

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();
        var diagnostic = result.BenchmarkReport!.Processes.Single(x => x.ProcessId == 124);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(diagnostic.ExitCode).IsNull();
            await Assert.That(diagnostic.CheckFile).IsNotNull();
            await Assert
                .That(result.Failures.Any(x => x.InterruptionReason!.Contains("取得不能")))
                .IsTrue();
        }
    }

    [Test]
    public async Task 保存参照の最終照合に失敗する_完了へ確定せず保存済みrawを保つ()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());
        File.WriteAllText(
            Path.Combine(fixture.DirectoryPath, "source", "index.patch"),
            "保存後の変更"
        );

        // Act & Assert
        await Assert
            .That(() =>
                BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary])
            )
            .Throws<IOException>();
        var manifest = RunStore.Load(fixture.DirectoryPath);
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.CompletedAtUtc).IsNull();
            await Assert
                .That(File.Exists(Path.Combine(fixture.DirectoryPath, "raw", "bdn-output.txt")))
                .IsTrue();
        }
    }

    [Test]
    public async Task 全予定reportと前後検査と必須指標が成立する_保存完了したrunだけ成功になる()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();
        fixture.SaveConditions();
        fixture.Prepared.Store.SaveCheck(fixture.SuccessfulCheck());

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(manifest.CompletedAtUtc).IsNotNull();
            await Assert.That(manifest.Cases.Single().Status).IsEqualTo(ResultStatus.Succeeded);
            await Assert.That(manifest.Cases.Single().Failures).Count().IsEqualTo(0);
            await Assert.That(manifest.Cases.Single().Metrics.Time).IsNotNull();
            await Assert.That(manifest.Cases.Single().Metrics.Allocation).IsNotNull();
            await Assert.That(manifest.Cases.Single().Metrics.Gc).IsNotNull();
        }
    }

    [Test]
    public async Task Resultが1反復だけのSmoke_時間は理由付き取得不能でも割り当てとrawを残す()
    {
        // Arrange
        // タスク5.3の正常Smokeから取り出したActual・Result・GC
        using var fixture = new BenchmarkReportFixture([
            "WorkloadActual 1: 64 op, 43400.00 ns",
            "WorkloadResult 1: 64 op, 43100.00 ns",
            "// GC: 0 0 0 24576 64",
        ]);

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Metrics.Time).IsNull();
            await Assert.That(result.Metrics.TimeUnavailableReason).Contains("99.9%信頼区間");
            await Assert.That(result.Metrics.Allocation!.BytesPerOperation).IsEqualTo(384);
            await Assert.That(result.Metrics.Gc!.TotalOperations).IsEqualTo(64);
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(result.RawFiles.Count).IsEqualTo(2);
            await Assert
                .That(result.RawFiles.Any(x => x.RelativePath.EndsWith("-measurements.csv")))
                .IsTrue();
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("// GC: 0 0 0 0 0")]
    public async Task GCの観測または操作数がない_割り当てとGCを0で補完せず理由を残す(string? gcLine)
    {
        // Arrange
        var lines = BenchmarkReportFixture.ReadLines().Where(x => !x.StartsWith("// GC:")).ToList();
        if (gcLine is not null)
        {
            lines.Add(gcLine);
        }
        using var fixture = new BenchmarkReportFixture(lines);

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Metrics.Time).IsNotNull();
            await Assert.That(result.Metrics.Allocation).IsNull();
            await Assert.That(result.Metrics.AllocationUnavailableReason).Contains("GC観測操作数");
            await Assert.That(result.Metrics.Gc).IsNull();
            await Assert.That(result.Metrics.GcUnavailableReason).Contains("GC観測操作数");
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
        }
    }

    [Test]
    [Arguments(
        "// GC: 179 0 0 2818572288 8388608",
        2818572288L,
        8388608L,
        336.0,
        336.0,
        179L,
        0L,
        0L,
        0.021338462829589844,
        0.0,
        0.0
    )]
    [Arguments("// GC: 2 1 1 5 20", 5L, 20L, 0.25, 0.0, 2L, 1L, 1L, 100.0, 50.0, 50.0)]
    [Arguments("// GC: 1 2 3 5 2", 5L, 2L, 2.5, 2.0, 1L, 2L, 3L, 500.0, 1000.0, 1500.0)]
    public async Task 割り当てとGCを保存する_総量と丸め前後のbytesと別反復の観測範囲が残る(
        string gcLine,
        long totalBytes,
        long operations,
        double unrounded,
        double displayed,
        long gen0,
        long gen1,
        long gen2,
        double normalized0,
        double normalized1,
        double normalized2
    )
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture(
            BenchmarkReportFixture.ReadLines().Where(x => !x.StartsWith("// GC:")).Append(gcLine)
        );

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var result = RunStore.Load(fixture.DirectoryPath).Cases.Single();

        // Assert
        await Assert.That(result.Metrics.Allocation).IsNotNull();
        await Assert.That(result.Metrics.Gc).IsNotNull();
        await Assert.That(result.Metrics.Time).IsNotNull();
        var allocation = result.Metrics.Allocation!;
        var gc = result.Metrics.Gc!;
        using (Assert.Multiple())
        {
            await Assert.That(allocation.TotalAllocatedBytes).IsEqualTo(totalBytes);
            await Assert.That(allocation.TotalOperations).IsEqualTo(operations);
            await Assert.That(allocation.BytesPerOperation).IsEqualTo(unrounded);
            await Assert.That(allocation.DisplayedBytesPerOperation).IsEqualTo(displayed);
            await Assert
                .That(allocation.Scope)
                .IsEqualTo(AllocationScope.ProcessManagedAllocations);
            await Assert
                .That(allocation.Iteration)
                .IsEqualTo(ObservationIteration.SeparateFromTiming);
            await Assert.That(allocation.Rounding.DecimalPlaces).IsEqualTo(0);
            await Assert
                .That(allocation.Rounding.MidpointRounding)
                .IsEqualTo(MidpointRounding.ToEven);
            await Assert
                .That(allocation.Rounding.Description)
                .Contains("表示0は割り当て不存在を意味しない");
            await Assert.That(result.Metrics.AllocationUnavailableReason).IsNull();
            await Assert.That(result.Metrics.GcUnavailableReason).IsNull();
            await Assert.That(gc.TotalOperations).IsEqualTo(operations);
            await Assert.That(gc.Gen0.TotalCollections).IsEqualTo(gen0);
            await Assert.That(gc.Gen1.TotalCollections).IsEqualTo(gen1);
            await Assert.That(gc.Gen2.TotalCollections).IsEqualTo(gen2);
            await Assert.That(gc.Gen0.CollectionsPerThousandOperations).IsEqualTo(normalized0);
            await Assert.That(gc.Gen1.CollectionsPerThousandOperations).IsEqualTo(normalized1);
            await Assert.That(gc.Gen2.CollectionsPerThousandOperations).IsEqualTo(normalized2);
            await Assert.That(gc.Iteration).IsEqualTo(ObservationIteration.SeparateFromTiming);
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert
                .That(result.RawFiles.Any(x => x.RelativePath == "raw/bdn-output.txt"))
                .IsTrue();
            await Assert
                .That(
                    File.ReadAllText(Path.Combine(fixture.DirectoryPath, "raw", "bdn-output.txt"))
                )
                .Contains(gcLine);
        }
    }

    [Test]
    public async Task 固定reportを保存する_Resultの時間統計とActualを含むrawが同じcaseへ残る()
    {
        // Arrange
        using var fixture = new BenchmarkReportFixture();

        // Act
        BenchmarkRun.SaveReports(fixture.Request, fixture.Prepared, [fixture.Summary]);
        var manifest = RunStore.Load(fixture.DirectoryPath);
        var result = manifest.Cases.Single();

        // Assert
        await Assert.That(result.Metrics.Time).IsNotNull();
        var time = result.Metrics.Time!;
        var raw = File.ReadAllBytes(
            Path.Combine(fixture.DirectoryPath, time.RawMeasurements.RelativePath)
        );
        var rows = System
            .Text.Encoding.UTF8.GetString(raw)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var values = rows.Skip(1).Select(x => x.TrimEnd('\r').Split(',')).ToArray();
        var results = values.Where(x => x[2] == "Workload" && x[3] == "Result").ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(result.CaseId).IsEqualTo("invoke.single");
            await Assert.That(result.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(manifest.Status).IsEqualTo(ResultStatus.Incomplete);
            await Assert.That(time.MeasurementCount).IsEqualTo(15);
            await Assert.That(time.MeanNanosecondsPerOperation).IsEqualTo(76.04321956634521);
            await Assert.That(time.MedianNanosecondsPerOperation).IsEqualTo(75.50182342529297);
            await Assert
                .That(time.StandardDeviationNanosecondsPerOperation)
                .IsEqualTo(1.3306110771777684);
            await Assert.That(time.ConfidenceInterval.Lower).IsEqualTo(74.6207156172511);
            await Assert.That(time.ConfidenceInterval.Upper).IsEqualTo(77.46572351543934);
            await Assert.That(time.ConfidenceInterval.Level).IsEqualTo(0.999);
            await Assert.That(result.Metrics.TimeUnavailableReason).IsNull();
            await Assert
                .That(rows[0])
                .IsEqualTo(
                    "launch_index,iteration_index,iteration_mode,iteration_stage,operations,elapsed_nanoseconds"
                );
            await Assert.That(values.Length).IsEqualTo(88);
            await Assert
                .That(values.Count(x => x[2] == "Workload" && x[3] == "Actual"))
                .IsEqualTo(15);
            await Assert.That(results.Length).IsEqualTo(15);
            await Assert.That(results[0][0]).IsEqualTo("1");
            await Assert.That(results[0][1]).IsEqualTo("1");
            await Assert.That(results[0][4]).IsEqualTo("8388608");
            await Assert
                .That(
                    results.Average(x =>
                        double.Parse(x[5], CultureInfo.InvariantCulture)
                        / long.Parse(x[4], CultureInfo.InvariantCulture)
                    )
                )
                .IsEqualTo(76.04321956634521);
            await Assert
                .That(time.RawMeasurements.Sha256)
                .IsEqualTo(Convert.ToHexStringLower(SHA256.HashData(raw)));
            await Assert.That(result.RawFiles.Contains(time.RawMeasurements)).IsTrue();
        }
    }
}
