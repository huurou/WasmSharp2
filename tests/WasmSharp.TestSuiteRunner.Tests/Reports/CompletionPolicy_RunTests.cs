using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CompletionPolicy_RunTests
{
    private static readonly string[] expectedReasons_ =
                    [
                        "入力単位のrunner_errorが2件あります。",
                        "操作全体の診断が1件あります。",
                    ];

    [Test]
    public async Task 複数入力の異常と操作全体の診断がある_各種類の合計件数を報告する()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed),
            RunReportFixture.Case("b.wast", 0, CaseCategory.Setup, CaseOutcome.Passed)
        );
        report.Inputs[0].Issues.Add(new("read", "aの異常"));
        report.Inputs[1].Issues.Add(new("read", "bの異常"));
        report.Diagnostics.Add(new("run", "操作の異常"));
        report = report with { Summary = report.Summarize() };

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert
                .That(decision.Reasons)
                .IsEquivalentTo(
                    expectedReasons_
                );
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Blockedの起点に未対応以外の結果が混在する_一部が未対応でも不合格理由を残す(
        bool duplicateOrigin
    )
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.RuntimeUnsupported),
            RunReportFixture.Case(
                "a.wast",
                duplicateOrigin ? 0 : 1,
                CaseCategory.Setup,
                CaseOutcome.Passed
            ),
            RunReportFixture.Case("a.wast", 2, CaseCategory.Action, CaseOutcome.Blocked) with
            {
                Cause = new CaseCause
                {
                    Direct = [new("a.wast", 0), new("a.wast", 1)],
                    Origins = duplicateOrigin
                        ? [new("a.wast", 0)]
                        : [new("a.wast", 0), new("a.wast", 1)],
                },
            }
        );

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        await Assert
            .That(decision.Reasons)
            .Contains("runtime_unsupported以外を原因とするblockedが1件あります。");
    }

    [Test]
    public async Task 未対応と対象外とその未対応を原因とするblockedだけが残る_成功の0を返す()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed),
            RunReportFixture.Case("a.wast", 1, CaseCategory.Setup, CaseOutcome.RuntimeUnsupported),
            RunReportFixture.Case("a.wast", 2, CaseCategory.Assertion, CaseOutcome.Blocked) with
            {
                Cause = new CaseCause { Direct = [new("a.wast", 1)], Origins = [new("a.wast", 1)] },
            },
            RunReportFixture.Case("a.wast", 3, CaseCategory.Assertion, CaseOutcome.OutOfScope)
        );

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(0);
            await Assert.That(decision.Reasons).IsEmpty();
        }
    }

    [Test]
    public async Task Failedと種類未確定のrunner_errorを全件記録した_用途不合格の1を返す()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Any(x => x.StartsWith("failedが1件"))).IsTrue();
            await Assert
                .That(decision.Reasons.Any(x => x.StartsWith("command単位のrunner_errorが1件")))
                .IsTrue();
            await Assert.That(decision.Reasons.Any(x => x.Contains("blocked"))).IsFalse();
        }
    }

    [Test]
    public async Task Failedを原因とするblockedがある_blockedも不合格の理由にする()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Failed),
            RunReportFixture.Case("a.wast", 1, CaseCategory.Action, CaseOutcome.Blocked) with
            {
                Cause = new CaseCause { Direct = [new("a.wast", 0)], Origins = [new("a.wast", 0)] },
            }
        );

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert
                .That(
                    decision.Reasons.Any(x =>
                        x.StartsWith("runtime_unsupported以外を原因とするblockedが1件")
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 入力異常によりcommand件数が未確定の入力を記録した_用途不合格の1を返す()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed)
        );
        report.Inputs[0] = report.Inputs[0] with
        {
            CommandCount = null,
            Issues = [new("read_script", "JSON末尾が破損しています。", "modules/a.json")],
        };
        report = report with { Summary = report.Summarize() };

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Any(x => x.Contains("未確定"))).IsTrue();
            await Assert
                .That(decision.Reasons.Any(x => x.StartsWith("入力単位のrunner_errorが1件")))
                .IsTrue();
        }
    }

    [Test]
    public async Task 中断した入力とfailedが残る_未完了の2を優先し両方の理由を返す()
    {
        // Arrange
        var report = RunReportFixture.CreateSample();
        report.Inputs[1] = report.Inputs[1] with
        {
            Status = InputRunStatus.Incomplete,
            UnprocessedCount = 1,
            Cases = [report.Inputs[1].Cases[0]],
        };
        report = report with
        {
            Summary = report.Summarize(),
            Completion = new RunCompletion(false, true),
        };

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(2);
            await Assert.That(decision.Reasons.Any(x => x.Contains("b.wast"))).IsTrue();
            await Assert.That(decision.Reasons.Any(x => x.StartsWith("failedが1件"))).IsTrue();
        }
    }

    [Test]
    public async Task 全件passedだが結果を保存できなかった_未完了の2を返す()
    {
        // Arrange
        var report = RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed)
        );

        // Act
        var decision = CompletionPolicy.Run(report, ReportStore.Validate(report), saved: false);

        // Assert
        await Assert.That(decision.ExitCode).IsEqualTo(2);
    }
}
