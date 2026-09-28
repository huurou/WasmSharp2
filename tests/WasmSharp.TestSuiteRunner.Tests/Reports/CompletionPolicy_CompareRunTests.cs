using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CompletionPolicy_CompareRunTests
{
    [Test]
    public async Task 比較が成立し回帰と現結果のfailedとrunner_errorがない_成功の0を返す()
    {
        // Arrange
        var report = CreateReport(new(0, 1, 0, 3, 2, 0, 0, 0, 0, 0));

        // Act
        var decision = CompletionPolicy.CompareRun(report, saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(0);
            await Assert.That(decision.Reasons).IsEmpty();
        }
    }

    [Test]
    [Arguments(1, 0, 0)]
    [Arguments(0, 1, 0)]
    [Arguments(0, 0, 1)]
    public async Task 回帰または既知のfailedまたはrunner_errorが残る_回帰0でも用途不合格の1を返す(
        int regressionCount,
        int currentFailedCount,
        int currentRunnerErrorCount
    )
    {
        // Arrange
        var report = CreateReport(
            new(0, 0, 0, 0, 0, 0, regressionCount, 0, currentFailedCount, currentRunnerErrorCount)
        );

        // Act
        var decision = CompletionPolicy.CompareRun(report, saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Single()).Contains("1件");
        }
    }

    [Test]
    [Arguments(false, true, 0)]
    [Arguments(true, false, 0)]
    [Arguments(true, true, 1)]
    public async Task 比較が成立しないか完了しないか未比較の対象がある_回帰なしとせず未完了の2を返す(
        bool established,
        bool complete,
        int uncomparedCount
    )
    {
        // Arrange
        var report = CreateReport(new(0, 0, 0, 0, 0, 0, 0, uncomparedCount, 0, 0)) with
        {
            Established = established,
            Complete = complete,
        };

        // Act
        var decision = CompletionPolicy.CompareRun(report, saved: true);

        // Assert
        await Assert.That(decision.ExitCode).IsEqualTo(2);
    }

    private static ComparisonReport CreateReport(ComparisonSummary summary)
    {
        return new ComparisonReport(
            ComparisonType.Run,
            new("/baseline/run.json") { RunId = "run-1" },
            new("/current/run.json") { RunId = "run-2" }
        )
        {
            Established = true,
            Complete = true,
            Summary = summary,
        };
    }
}
