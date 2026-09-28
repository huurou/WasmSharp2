using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_ValidateTests
{
    [Test]
    public async Task 全件を記録した結果を検証する_failedやrunner_errorを含んでも記録の問題を報告しない()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();

        // Act
        var runIssues = ReportStore.Validate(report);
        var manifestIssues = ReportStore.Validate(report.Corpus);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(runIssues).IsEmpty();
            await Assert.That(manifestIssues).IsEmpty();
        }
    }

    [Test]
    public async Task 実行結果の入力がprofileと一致しない_欠落と重複と対象外を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[1] = report.Inputs[0];
        report.Inputs.Add(
            new InputRunResult("z.wast") { Status = InputRunStatus.Processed, CommandCount = 0 }
        );
        report = report with { Summary = report.Summarize() };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(HasIssue(issues, RecordIssueKind.Missing, "b.wast")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Duplicate, "a.wast")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Unexpected, "z.wast")).IsTrue();
        }
    }

    [Test]
    public async Task ケースが素材のcommand一覧と一致しない_欠落と重複と範囲外と件数不一致を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        var cases = report.Inputs[0].Cases;
        report.Inputs[0] = report.Inputs[0] with
        {
            Cases =
            [
                cases[0],
                cases[2],
                cases[2],
                cases[3],
                cases[3] with
                {
                    Id = new("a.wast", 4),
                },
            ],
        };
        report.Inputs[1] = report.Inputs[1] with { CommandCount = 3, EnumeratedCount = 3 };
        report = report with { Summary = report.Summarize() };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(HasIssue(issues, RecordIssueKind.Missing, "a.wast#1")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Duplicate, "a.wast#2")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Unexpected, "a.wast#4")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Inconsistent, "b.wast")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Missing, "b.wast#2")).IsTrue();
        }
    }

    [Test]
    public async Task 素材と同じcommand件数だが列挙済み数が一致しない_不整合を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[1] = report.Inputs[1] with { EnumeratedCount = 0, Cases = [] };
        report = report with { Summary = report.Summarize() };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        await Assert.That(HasIssue(issues, RecordIssueKind.Inconsistent, "b.wast")).IsTrue();
    }

    [Test]
    public async Task 素材のcommand一覧が保存された件数より多い_件数を信用せず不整合を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        var artifact = report.Corpus.Inputs[0].Artifacts[0];
        report.Corpus.Inputs[0].Artifacts[0] = artifact with
        {
            Script = artifact.Script! with
            {
                Commands = [.. artifact.Script.Commands, new(4, 5, "module", null, null)],
            },
        };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        await Assert
            .That(HasIssue(issues, RecordIssueKind.Inconsistent, "a.wastのcommand件数"))
            .IsTrue();
    }

    [Test]
    public async Task 素材スナップショットの入力が重複する_素材の重複と集計不整合を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Corpus.Inputs.Add(report.Corpus.Inputs[0]);

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(HasIssue(issues, RecordIssueKind.Duplicate, "素材のa.wast")).IsTrue();
            await Assert
                .That(HasIssue(issues, RecordIssueKind.Inconsistent, "素材の集計"))
                .IsTrue();
        }
    }

    [Test]
    public async Task 完了flagがあるが中断した入力が残る_未処理と完了情報の不整合を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[1] = report.Inputs[1] with
        {
            Status = InputRunStatus.Incomplete,
            UnprocessedCount = 1,
            Cases = [report.Inputs[1].Cases[0]],
        };
        report = report with { Summary = report.Summarize() };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(HasIssue(issues, RecordIssueKind.Unprocessed, "b.wast")).IsTrue();
            await Assert
                .That(
                    HasIssue(issues, RecordIssueKind.Inconsistent, "処理の完了が記録されています")
                )
                .IsTrue();
            await Assert.That(issues.Any(x => x.Kind == RecordIssueKind.Missing)).IsFalse();
        }
    }

    [Test]
    public async Task 内容は揃うが完了と出力が記録されていない_未処理と出力失敗を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample() with
        {
            Completion = new(false, false),
        };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(issues.Count(x => x.Kind == RecordIssueKind.Unprocessed))
                .IsEqualTo(1);
            await Assert
                .That(issues.Count(x => x.Kind == RecordIssueKind.OutputFailed))
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task 件数未確定と種類未確定の分類と集計が不正である_未確定と不整合を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[0] = report.Inputs[0] with { CommandCount = null };
        report.Inputs[0].Cases[3] = report.Inputs[0].Cases[3] with { Outcome = CaseOutcome.Passed };

        // Act
        var issues = ReportStore.Validate(report);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(HasIssue(issues, RecordIssueKind.Undetermined, "a.wast")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Inconsistent, "a.wast#3")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Inconsistent, "集計")).IsTrue();
        }
    }

    [Test]
    public async Task 部分生成のmanifestを検証する_未処理の入力と完了情報の欠落を報告する()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create();

        // Act
        var issues = ReportStore.Validate(manifest);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(HasIssue(issues, RecordIssueKind.Unprocessed, "unprocessed.wast"))
                .IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Unprocessed, "処理の完了")).IsTrue();
            await Assert.That(issues.Count).IsEqualTo(2);
        }
    }

    [Test]
    public async Task Manifestの入力欠落と集計不整合と出力未完了がある_それぞれを報告する()
    {
        // Arrange
        var manifest = RunReportFixture.CreateSample().Corpus;
        manifest.Inputs.RemoveAt(1);
        manifest = manifest with
        {
            Summary = new ConversionSummary(2, 2, 0, 0, 2),
            Completion = new ConversionCompletion(true, false),
        };

        // Act
        var issues = ReportStore.Validate(manifest);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(HasIssue(issues, RecordIssueKind.Missing, "b.wast")).IsTrue();
            await Assert.That(HasIssue(issues, RecordIssueKind.Inconsistent, "集計")).IsTrue();
            await Assert.That(issues.Any(x => x.Kind == RecordIssueKind.OutputFailed)).IsTrue();
        }
    }

    private static bool HasIssue(IEnumerable<RecordIssue> issues, RecordIssueKind kind, string text)
    {
        return issues.Any(x =>
            x.Kind == kind && x.Message.Contains(text, StringComparison.Ordinal)
        );
    }
}
