using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_AttachChecksTests
{
    private readonly CaseJson json_ = new CaseJson();
    private readonly MeasurementMethods methods_ = new MeasurementMethods();
    private readonly BenchmarkRun benchmarkRun_;

    public BenchmarkRun_AttachChecksTests()
    {
        benchmarkRun_ = new BenchmarkRun(json_, methods_);
    }

    [Test]
    [Arguments(RunStage.Precheck)]
    [Arguments(RunStage.ExecutionStarted)]
    [Arguments(RunStage.Postcheck)]
    public async Task 同じprocessに異なる段階の検査がある_より進んだ段階を採用する(RunStage stage)
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);
        var check = fixture.Check;
        var earlier = stage switch
        {
            RunStage.Precheck => check with
            {
                Precheck = null,
                ExecutionStarted = false,
                Postcheck = null,
            },
            RunStage.ExecutionStarted => check with { ExecutionStarted = false, Postcheck = null },
            _ => check with { Postcheck = null },
        };
        var later = stage switch
        {
            RunStage.Precheck => check with { ExecutionStarted = false, Postcheck = null },
            RunStage.ExecutionStarted => check with { Postcheck = null },
            _ => check,
        };
        store.SaveCheck(
            earlier with
            {
                Failures =
                [
                    new(
                        check.CaseId,
                        RunStage.Precheck,
                        null,
                        "以前の段階の失敗",
                        null,
                        null,
                        null
                    ),
                ],
            }
        );
        var expected = store.SaveCheck(later);

        // Act
        var results = benchmarkRun_.AttachChecks(
            fixture.DirectoryPath,
            check.RunId,
            check.CaseId,
            check.JobId,
            [new(check.JobId, check.ProcessId, 0, [], true, null)]
        );

        // Assert
        await Assert.That(results.Single().CheckFile).IsEqualTo(expected);
    }

    [Test]
    public async Task 同じ段階の検査へ失敗が追記される_失敗を保持した検査を採用する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);
        var check = fixture.Check;
        store.SaveCheck(check);
        var expected = store.SaveCheck(
            check with
            {
                Failures =
                [
                    new(
                        check.CaseId,
                        RunStage.Postcheck,
                        RunStage.ExecutionStarted,
                        "後検査の失敗",
                        null,
                        null,
                        null
                    ),
                ],
            }
        );

        // Act
        var results = benchmarkRun_.AttachChecks(
            fixture.DirectoryPath,
            check.RunId,
            check.CaseId,
            check.JobId,
            [new(check.JobId, check.ProcessId, 1, [], false, null)]
        );

        // Assert
        await Assert.That(results.Single().CheckFile).IsEqualTo(expected);
    }

    [Test]
    [Arguments("run")]
    [Arguments("case")]
    [Arguments("job")]
    public async Task 異なるrunまたはcaseまたはJobを指定する_検査を診断processとして追加しない(
        string different
    )
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);
        var check = fixture.Check;
        store.SaveCheck(check);

        // Act
        var results = benchmarkRun_.AttachChecks(
            fixture.DirectoryPath,
            different == "run" ? "other-run" : check.RunId,
            different == "case" ? "other-case" : check.CaseId,
            different == "job" ? "other-job" : check.JobId,
            []
        );

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task 検査がなくprocess番号も取得できない_元の終了情報を検査なしで保持する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var check = fixture.Check;
        var processes = new BenchmarkProcessResult[]
        {
            new(check.JobId, null, 1, ["process番号なし"], false, null),
            new(check.JobId, check.ProcessId, 0, [], true, null),
        };

        // Act
        var results = benchmarkRun_.AttachChecks(
            fixture.DirectoryPath,
            check.RunId,
            check.CaseId,
            check.JobId,
            processes
        );

        // Assert
        await Assert.That(results).IsEquivalentTo(processes);
    }

    [Test]
    public async Task 同じcaseに別Jobと診断processがある_最新検査をprocessごとに対応付ける()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);
        var check = fixture.Check;
        store.SaveCheck(
            check with
            {
                Postcheck = null,
                LastConfirmedStage = RunStage.ExecutionStarted,
            }
        );
        var final = store.SaveCheck(check);
        store.SaveCheck(check with { JobId = "OtherJob" });
        var diagnostic = store.SaveCheck(check with { ProcessId = check.ProcessId + 1 });

        // Act
        var results = benchmarkRun_.AttachChecks(
            fixture.DirectoryPath,
            check.RunId,
            check.CaseId,
            check.JobId,
            [new(check.JobId, check.ProcessId, 0, [], true, null)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Count).IsEqualTo(2);
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId).CheckFile)
                .IsEqualTo(final);
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId + 1).CheckFile)
                .IsEqualTo(diagnostic);
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId + 1).ExitCode)
                .IsNull();
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId + 1).HasResultMeasurements)
                .IsFalse();
        }
    }
}
