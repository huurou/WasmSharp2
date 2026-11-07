using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CompletionPolicy_BaselineSaveTests
{
    [Test]
    public async Task 同じ種類の記録不備が複数ある_種類ごとの代表例と残り件数を報告する()
    {
        // Arrange
        var issues = System.Collections.Immutable.ImmutableArray.Create(
            new RecordIssue(RecordIssueKind.Undetermined, "a.wastの件数が未確定です。"),
            new RecordIssue(RecordIssueKind.Undetermined, "b.wastの件数が未確定です。"),
            new RecordIssue(RecordIssueKind.Undetermined, "c.wastの件数が未確定です。")
        );

        // Act
        var decision = CompletionPolicy.BaselineSave(issues, saved: true);
        var single = CompletionPolicy.BaselineSave([issues[0]], saved: true);
        var unsaved = CompletionPolicy.BaselineSave([], saved: false);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(2);
            await Assert
                .That(decision.Reasons.Single())
                .IsEqualTo("a.wastの件数が未確定です。ほか2件");
            await Assert.That(single.Reasons.Single()).IsEqualTo("a.wastの件数が未確定です。");
            await Assert.That(unsaved.ExitCode).IsEqualTo(2);
            await Assert.That(unsaved.Reasons.Single()).Contains("保存");
        }
    }

    [Test]
    public async Task Failedやrunner_errorやblockedを含む全件記録済みの結果を保存した_成功の0を返す()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();

        // Act
        var decision = CompletionPolicy.BaselineSave(ReportStore.Validate(report), saved: true);

        // Assert
        await Assert.That(decision.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task 件数未確定の入力が残る実行結果_保存を拒否する未完了の2を返す()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[0] = report.Inputs[0] with { CommandCount = null };
        report = report with { Summary = report.Summarize() };

        // Act
        var decision = CompletionPolicy.BaselineSave(ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(2);
            await Assert.That(decision.Reasons.Any(x => x.Contains("未確定"))).IsTrue();
        }
    }

    [Test]
    public async Task 未処理の入力と出力未完了が残るmanifest_保存を拒否する未完了の2を返す()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create() with
        {
            Completion = new(false, false),
        };

        // Act
        var decision = CompletionPolicy.BaselineSave(ReportStore.Validate(manifest), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(2);
            await Assert.That(decision.Reasons.Any(x => x.Contains("unprocessed.wast"))).IsTrue();
            await Assert.That(decision.Reasons.Any(x => x.Contains("出力"))).IsTrue();
        }
    }
}
