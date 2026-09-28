using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CompletionPolicy_CompareConversionTests
{
    [Test]
    public async Task 変換器実行ファイルのhashだけが異なる_出典差異として成功の0を返す()
    {
        // Arrange
        var report = CreateReport(new(0, 1, 0, 0, 0, 0, 0, 0, 0, 0));

        // Act
        var decision = CompletionPolicy.CompareConversion(report, saved: true);

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
    public async Task 条件差や生成物差や現結果のrunner_errorがある_用途不合格の1を返す(
        int conditionDifferenceCount,
        int entryDifferenceCount,
        int currentRunnerErrorCount
    )
    {
        // Arrange
        var report = CreateReport(
            new(
                conditionDifferenceCount,
                0,
                entryDifferenceCount,
                0,
                0,
                0,
                0,
                0,
                0,
                currentRunnerErrorCount
            )
        );

        // Act
        var decision = CompletionPolicy.CompareConversion(report, saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Single()).Contains("1件");
        }
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task 比較が完了していないか比較結果を保存できなかった_未完了の2を返す(
        bool complete,
        bool saved
    )
    {
        // Arrange
        var report = CreateReport(new(1, 0, 0, 0, 0, 0, 0, 0, 0, 0)) with
        {
            Complete = complete,
        };

        // Act
        var decision = CompletionPolicy.CompareConversion(report, saved);

        // Assert
        await Assert.That(decision.ExitCode).IsEqualTo(2);
    }

    private static ComparisonReport CreateReport(ComparisonSummary summary)
    {
        return new ComparisonReport(
            ComparisonType.Conversion,
            new("/baseline/manifest.json"),
            new("/current/manifest.json")
        )
        {
            Established = true,
            Complete = true,
            Summary = summary,
        };
    }
}
