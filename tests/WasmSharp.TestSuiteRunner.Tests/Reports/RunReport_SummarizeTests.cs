using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class RunReport_SummarizeTests
{
    [Test]
    public async Task 同一行の複数ケースと種類未確定commandを含む_区分別の6分類と種類未確定を重複なく数える()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();

        // Act
        var summary = report.Summarize();

        // Assert
        var total = new[] { summary.Setup, summary.Action, summary.Assertion }.Sum(x =>
            x.Passed + x.Failed + x.RuntimeUnsupported + x.RunnerError + x.OutOfScope + x.Blocked
        );
        using (Assert.Multiple())
        {
            await Assert
                .That(summary)
                .IsEqualTo(
                    new RunSummary(
                        2,
                        2,
                        0,
                        0,
                        0,
                        0,
                        0,
                        6,
                        0,
                        new(1, 0, 1, 0, 0, 0),
                        new(0, 0, 0, 0, 0, 1),
                        new(1, 1, 0, 0, 0, 0),
                        1
                    )
                );
            await Assert.That(total + summary.UncategorizedRunnerErrorCount).IsEqualTo(6);
        }
    }

    [Test]
    public async Task 中断と件数未確定と入力異常を含む_入力とcommandの状態を6分類とは別に数える()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[0] = report.Inputs[0] with
        {
            Status = InputRunStatus.Incomplete,
            CommandCount = null,
            UnprocessedCount = 2,
            Cases = [.. report.Inputs[0].Cases.Take(2)],
            Issues =
            [
                new("verify_artifact", "wat素材のhashが一致しません。", "modules/a.0.wat"),
                new("read_script", "JSON末尾が破損しています。", "modules/a.json"),
            ],
        };
        report.Inputs[1] = new InputRunResult("b.wast");

        // Act
        var summary = report.Summarize();

        // Assert
        OutcomeCounts zero = new(0, 0, 0, 0, 0, 0);
        await Assert
            .That(summary)
            .IsEqualTo(new RunSummary(2, 0, 1, 1, 1, 1, 2, 4, 2, new(1, 0, 0, 0, 0, 0), zero, new(1, 0, 0, 0, 0, 0), 0));
    }

    [Test]
    public async Task 複数入力に未処理commandと異常がある_入力ごとの件数を合計する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[0] = report.Inputs[0] with
        {
            Status = InputRunStatus.Incomplete,
            UnprocessedCount = 2,
            Cases = [.. report.Inputs[0].Cases.Take(2)],
            Issues = [new("verify", "aの入力異常")],
        };
        report.Inputs[1] = report.Inputs[1] with
        {
            Status = InputRunStatus.Incomplete,
            UnprocessedCount = 1,
            Cases = [report.Inputs[1].Cases[0] with { Outcome = CaseOutcome.RunnerError }],
            Issues = [new("verify", "bの入力異常1"), new("verify", "bの入力異常2")],
        };

        // Act
        var summary = report.Summarize();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary.InputIssueCount).IsEqualTo(3);
            await Assert.That(summary.UnprocessedCommandCount).IsEqualTo(3);
            await Assert.That(summary.Setup.RunnerError).IsEqualTo(1);
            await Assert.That(summary.UncategorizedRunnerErrorCount).IsEqualTo(0);
        }
    }
}
