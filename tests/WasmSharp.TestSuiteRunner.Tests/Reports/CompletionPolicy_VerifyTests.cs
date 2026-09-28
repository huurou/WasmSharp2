using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CompletionPolicy_VerifyTests
{
    [Test]
    public async Task 固定全入力と全commandを記録し対象外以外が全てpassed_成功の0を返す()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed),
            RunReportFixture.Case("a.wast", 1, CaseCategory.Assertion, CaseOutcome.OutOfScope),
            RunReportFixture.Case("b.wast", 0, CaseCategory.Action, CaseOutcome.Passed)
        );

        // Act
        var decision = CompletionPolicy.Verify(
            report,
            ReportStore.Validate(report),
            report.Corpus.Profile.ToProfile()
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(0);
            await Assert.That(decision.Reasons).IsEmpty();
        }
    }

    [Test]
    public async Task 未対応やblockedや失敗が残る_用途不合格の1で残る分類を報告する()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();

        // Act
        var decision = CompletionPolicy.Verify(
            report,
            ReportStore.Validate(report),
            report.Corpus.Profile.ToProfile()
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Any(x => x.StartsWith("failedが1件"))).IsTrue();
            await Assert
                .That(decision.Reasons.Any(x => x.StartsWith("runtime_unsupportedが1件")))
                .IsTrue();
            await Assert
                .That(decision.Reasons.Any(x => x.StartsWith("command単位のrunner_errorが1件")))
                .IsTrue();
            await Assert.That(decision.Reasons.Any(x => x.StartsWith("blockedが1件"))).IsTrue();
        }
    }

    [Test]
    public async Task 欠落と件数未確定と入力異常が残る_全passedでも用途不合格の1を返す()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed),
            RunReportFixture.Case("a.wast", 1, CaseCategory.Assertion, CaseOutcome.Passed)
        );
        report.Inputs[0] = report.Inputs[0] with
        {
            CommandCount = null,
            Cases = [report.Inputs[0].Cases[0]],
            Issues = [new("verify_artifact", "JSONのhashが一致しません。", "modules/a.json")],
        };
        report = report with { Summary = report.Summarize() };

        // Act
        var decision = CompletionPolicy.Verify(
            report,
            ReportStore.Validate(report),
            report.Corpus.Profile.ToProfile()
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Any(x => x.Contains("a.wast#1"))).IsTrue();
            await Assert.That(decision.Reasons.Any(x => x.Contains("未確定"))).IsTrue();
            await Assert
                .That(decision.Reasons.Any(x => x.StartsWith("入力単位のrunner_errorが1件")))
                .IsTrue();
        }
    }

    [Test]
    public async Task 固定profileと異なる入力集合の結果_全passedでも用途不合格の1を返す()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed)
        );

        // Act
        var decision = CompletionPolicy.Verify(
            report,
            ReportStore.Validate(report),
            Core2Profile.Load()
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Single()).Contains("core2");
        }
    }
}
