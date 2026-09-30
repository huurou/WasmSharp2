using System.Text.Json;
using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Baselines;

internal class BaselineComparer_CompareRunTests
{
    [Test]
    public async Task 内容が同じで実行IDや日時や版が異なる_入力pathとindexで対応付け変化なしとする()
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport() with
        {
            Provenance = new("current-run")
            {
                StartedAt = DateTimeOffset.UtcNow,
                RuntimeVersion = "changed",
                RunnerVersion = "changed",
                ManifestPath = "/moved/manifest.json",
            },
        };
        current.Inputs.Reverse();
        current.Inputs[1].Cases.Reverse();

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Comparison).IsEqualTo(ComparisonType.Run);
            await Assert.That(comparison.Established).IsTrue();
            await Assert.That(comparison.Complete).IsTrue();
            await Assert.That(comparison.Baseline.RunId).IsEqualTo("fixture-run");
            await Assert.That(comparison.Current.RunId).IsEqualTo("current-run");
            await Assert.That(comparison.Cases.Count).IsEqualTo(3);
            await Assert.That(comparison.Cases.All(x => x.Change == CaseChange.Unchanged)).IsTrue();
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(0);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(CaseOutcome.Failed)]
    [Arguments(CaseOutcome.RuntimeUnsupported)]
    [Arguments(CaseOutcome.RunnerError)]
    [Arguments(CaseOutcome.OutOfScope)]
    [Arguments(CaseOutcome.Blocked)]
    public async Task 以前passedだったケースが別分類になる_前後の結果と回帰を残す(
        CaseOutcome outcome
    )
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        current.Inputs[0].Cases[1] = current.Inputs[0].Cases[1] with { Outcome = outcome };
        current = current with { Summary = current.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        var changed = comparison.Cases.Single(x => x.Id == new CaseId("a.wast", 1));
        using (Assert.Multiple())
        {
            await Assert.That(changed.Change).IsEqualTo(CaseChange.Changed);
            await Assert.That(changed.Regression).IsTrue();
            await Assert.That(changed.Baseline!.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(changed.Current!.Outcome).IsEqualTo(outcome);
            await Assert.That(comparison.Summary.ChangedCaseCount).IsEqualTo(1);
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(1);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("期待値")]
    [Arguments("期待lane")]
    [Arguments("期待診断")]
    [Arguments("実値")]
    [Arguments("段階")]
    [Arguments("診断")]
    [Arguments("診断位置")]
    [Arguments("未確認範囲")]
    [Arguments("print")]
    public async Task 分類が同じで期待や実際や診断が変化する_詳細差分を残し分類の回帰と区別する(
        string change
    )
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        var item = current.Inputs[0].Cases[1];
        switch (change)
        {
            case "期待値":
                item = item with { ExpectedValues = [new("i32") { Value = "1" }] };
                break;
            case "期待lane":
                item.ExpectedValues[0].Lanes[0] = "1";
                break;
            case "期待診断":
                item = item with { ExpectedText = "changed diagnostic" };
                break;
            case "実値":
                item.ActualValues[0] = item.ActualValues[0] with { Low64 = "0000000000000001" };
                break;
            case "段階":
                item = item with { LastStage = CaseStage.Instantiate };
                break;
            case "診断":
                item.Diagnostics[0] = item.Diagnostics[0] with { Message = "変更後の診断" };
                break;
            case "診断位置":
                item.Diagnostics[0] = item.Diagnostics[0] with
                {
                    Location = new("Invoke", 99, 0, null),
                };
                break;
            case "未確認範囲":
                item.Diagnostics[0].UnverifiedRanges.Add(new("Validate", 0, 1, "変更後の範囲"));
                break;
            case "print":
                item.Prints[0].Arguments[0] = new("i32") { Bits = "00000001" };
                break;
        }
        current.Inputs[0].Cases[1] = item;

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        var changed = comparison.Cases.Single(x => x.Id == item.Id);
        using (Assert.Multiple())
        {
            await Assert.That(changed.Change).IsEqualTo(CaseChange.Changed);
            await Assert.That(changed.Regression).IsFalse();
            await Assert
                .That(
                    JsonElement.DeepEquals(
                        JsonSerializer.SerializeToElement(changed.Baseline),
                        JsonSerializer.SerializeToElement(baseline.Inputs[0].Cases[1])
                    )
                )
                .IsTrue();
            await Assert
                .That(
                    JsonElement.DeepEquals(
                        JsonSerializer.SerializeToElement(changed.Current),
                        JsonSerializer.SerializeToElement(item)
                    )
                )
                .IsTrue();
            await Assert.That(comparison.Summary.ChangedCaseCount).IsEqualTo(1);
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 以前passedだった結果が欠落する_回帰と未完了を併記する()
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        current.Inputs[0].Cases.RemoveAt(1);
        current = current with { Summary = current.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        var missing = comparison.Cases.Single(x => x.Id == new CaseId("a.wast", 1));
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Established).IsTrue();
            await Assert.That(comparison.Complete).IsFalse();
            await Assert.That(missing.Change).IsEqualTo(CaseChange.Missing);
            await Assert.That(missing.Regression).IsTrue();
            await Assert.That(missing.Current).IsNull();
            await Assert.That(comparison.Summary.MissingCaseCount).IsEqualTo(1);
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(1);
            await Assert
                .That(
                    comparison.Uncompared.Any(x =>
                        x.Reason.Contains("a.wast#1", StringComparison.Ordinal)
                        || x.Target == "a.wast#1"
                    )
                )
                .IsTrue();
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task 以前failedだった結果が欠落する_差分と未完了を残し回帰とは数えない()
    {
        // Arrange
        var baseline = CreateReport();
        baseline.Inputs[0].Cases[1] = baseline.Inputs[0].Cases[1] with
        {
            Outcome = CaseOutcome.Failed,
        };
        baseline = baseline with { Summary = baseline.Summarize() };
        var current = CreateReport();
        current.Inputs[0].Cases.RemoveAt(1);
        current = current with { Summary = current.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Summary.MissingCaseCount).IsEqualTo(1);
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(0);
            await Assert.That(comparison.Complete).IsFalse();
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task Currentにだけケースが存在する_追加として結果を残す()
    {
        // Arrange
        var baseline = CreateReport();
        baseline.Inputs[0].Cases.RemoveAt(1);
        baseline = baseline with { Summary = baseline.Summarize() };
        var current = CreateReport();

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        var added = comparison.Cases.Single(x => x.Id == new CaseId("a.wast", 1));
        using (Assert.Multiple())
        {
            await Assert.That(added.Change).IsEqualTo(CaseChange.Added);
            await Assert.That(added.Baseline).IsNull();
            await Assert.That(added.Current!.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(added.Regression).IsFalse();
            await Assert.That(comparison.Summary.AddedCaseCount).IsEqualTo(1);
            await Assert.That(comparison.Complete).IsFalse();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task 片側のケース識別が重複する_対応不能の理由を残し他のケースは比較する(
        bool baselineDuplicate
    )
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        var duplicated = baselineDuplicate ? baseline : current;
        duplicated.Inputs[0].Cases.Add(duplicated.Inputs[0].Cases[1]);
        if (baselineDuplicate)
        {
            baseline = baseline with { Summary = baseline.Summarize() };
        }
        else
        {
            current = current with { Summary = current.Summarize() };
        }

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Cases.Any(x => x.Id == new CaseId("a.wast", 1))).IsFalse();
            await Assert.That(comparison.Cases.Count).IsEqualTo(2);
            await Assert
                .That(
                    comparison.Uncompared.Any(x =>
                        x.Target == "a.wast#1"
                        && x.Reason.Contains("重複", StringComparison.Ordinal)
                    )
                )
                .IsTrue();
            await Assert.That(comparison.Complete).IsFalse();
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    [Arguments("profile")]
    [Arguments("feature")]
    [Arguments("入力hash")]
    [Arguments("生成物hash")]
    [Arguments("生成物欠落")]
    public async Task 比較成立の条件が異なる_条件差と未比較理由を残し回帰を判定しない(
        string condition
    )
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        switch (condition)
        {
            case "profile":
                current = current with
                {
                    Corpus = current.Corpus with
                    {
                        Profile = current.Corpus.Profile with { Id = "changed" },
                    },
                };
                break;
            case "feature":
                current.Corpus.Profile.Features[0] = current.Corpus.Profile.Features[0] with
                {
                    Enabled = !current.Corpus.Profile.Features[0].Enabled,
                };
                break;
            case "入力hash":
                current.Corpus.Profile.Inputs[0] = current.Corpus.Profile.Inputs[0] with
                {
                    Sha256 = new string('f', 64),
                };
                break;
            case "生成物hash":
                current.Corpus.Inputs[0].Artifacts[0] = current.Corpus.Inputs[0].Artifacts[0] with
                {
                    Sha256 = new string('f', 64),
                };
                break;
            case "生成物欠落":
                current.Corpus.Inputs[0].Artifacts.RemoveAt(0);
                current = current with
                {
                    Corpus = current.Corpus with { Summary = current.Corpus.Summarize() },
                };
                break;
        }

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Established).IsFalse();
            await Assert.That(comparison.Complete).IsFalse();
            await Assert
                .That(comparison.ConditionDifferences.Count + comparison.EntryDifferences.Count)
                .IsGreaterThan(0);
            await Assert.That(comparison.Uncompared).IsNotEmpty();
            await Assert.That(comparison.Cases).IsEmpty();
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task 実行ファイルhashと配置先だけが変わる_出典差を残し実行比較を継続する()
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport() with
        {
            Corpus = baseline.Corpus.CreateSnapshot() with
            {
                Provenance = baseline.Corpus.Provenance with
                {
                    ExecutableSha256 = new string('e', 64),
                    OutputRoot = "/moved",
                },
            },
        };

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Established).IsTrue();
            await Assert.That(comparison.Complete).IsTrue();
            await Assert.That(comparison.Summary.ProvenanceDifferenceCount).IsEqualTo(1);
            await Assert.That(comparison.Cases.Count).IsEqualTo(3);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(0);
        }
    }

    [Test]
    public async Task 両結果に既知のfailedが残る_回帰0でも非0を返し詳細を保持する()
    {
        // Arrange
        var baseline = RunReportFixture.CreateSample();
        var current = RunReportFixture.CreateSample();

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        var failed = comparison.Cases.Single(x => x.Id == new CaseId("a.wast", 2));
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Complete).IsTrue();
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(0);
            await Assert.That(comparison.Summary.CurrentFailedCount).IsEqualTo(1);
            await Assert.That(comparison.Summary.CurrentRunnerErrorCount).IsEqualTo(1);
            await Assert.That(failed.Change).IsEqualTo(CaseChange.Unchanged);
            await Assert.That(failed.Current!.ExpectedValues[0].Value).IsEqualTo("1");
            await Assert.That(failed.Current.ActualValues[0].Bits).IsEqualTo("00000002");
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task 以前failedだったケースがpassedへ変わる_改善として残し回帰に数えない()
    {
        // Arrange
        var baseline = CreateReport();
        baseline.Inputs[0].Cases[1] = baseline.Inputs[0].Cases[1] with
        {
            Outcome = CaseOutcome.Failed,
        };
        baseline = baseline with { Summary = baseline.Summarize() };
        var current = CreateReport();

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Summary.ChangedCaseCount).IsEqualTo(1);
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(0);
            await Assert.That(comparison.Summary.CurrentFailedCount).IsEqualTo(0);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(0);
        }
    }

    [Test]
    public async Task 入力単位の異常が残る_ケース回帰0でもrunner_errorとして非0にする()
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        current.Inputs[0].Issues.Add(new("verify", "素材異常", "a.wast"));
        current = current with { Summary = current.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Summary.RegressionCount).IsEqualTo(0);
            await Assert.That(comparison.Summary.CurrentRunnerErrorCount).IsEqualTo(1);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("未処理")]
    [Arguments("未確定")]
    [Arguments("集計不整合")]
    [Arguments("出力失敗")]
    public async Task 結果の記録が未完了_比較可能な詳細を残し未完了とする(string issue)
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        switch (issue)
        {
            case "未処理":
                current.Inputs[0] = current.Inputs[0] with
                {
                    Status = InputRunStatus.Incomplete,
                    UnprocessedCount = 1,
                };
                current.Inputs[0].Cases.RemoveAt(1);
                break;
            case "未確定":
                current.Inputs[0] = current.Inputs[0] with { CommandCount = null };
                break;
            case "集計不整合":
                current = current with { Summary = RunSummary.Empty };
                break;
            case "出力失敗":
                current = current with { Completion = new(true, false) };
                break;
        }
        if (issue != "集計不整合")
        {
            current = current with { Summary = current.Summarize() };
        }

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Cases).IsNotEmpty();
            await Assert.That(comparison.Uncompared).IsNotEmpty();
            await Assert.That(comparison.Complete).IsFalse();
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task 比較後に元の一覧を変更する_返却済みの前後結果を変更しない()
    {
        // Arrange
        var baseline = CreateReport();
        var current = CreateReport();
        var source = current.Inputs[0].Cases[1];
        var blockedReport = RunReportFixture.CreateSample();

        // Act
        var comparison = BaselineComparer.CompareRun(baseline, current);
        var blockedComparison = BaselineComparer.CompareRun(
            blockedReport,
            RunReportFixture.CreateSample()
        );
        baseline.Inputs[0].Cases[1].ExpectedValues[0].Lanes.Clear();
        source.ExpectedValues.Clear();
        source.ActualValues.Clear();
        source.Diagnostics[0].UnverifiedRanges.Add(new("Validate", 1, 2, "後から追加"));
        source.Diagnostics.Clear();
        source.Prints[0].Arguments.Clear();
        source.Prints.Clear();
        blockedReport.Inputs[1].Cases[1].Cause!.Direct.Clear();
        blockedReport.Inputs[1].Cases[1].Cause!.Origins.Clear();

        // Assert
        var saved = comparison.Cases.Single(x => x.Id == source.Id);
        var blocked = blockedComparison.Cases.Single(x => x.Id == new CaseId("b.wast", 1));
        using (Assert.Multiple())
        {
            await Assert.That(saved.Baseline!.ExpectedValues[0].Lanes.Count).IsEqualTo(4);
            await Assert.That(saved.Current!.ExpectedValues.Count).IsEqualTo(1);
            await Assert.That(saved.Current.ActualValues.Count).IsEqualTo(1);
            await Assert.That(saved.Current.Diagnostics[0].UnverifiedRanges).IsEmpty();
            await Assert.That(saved.Current.Prints[0].Arguments.Count).IsEqualTo(1);
            await Assert.That(blocked.Baseline!.Cause!.Direct.Count).IsEqualTo(1);
            await Assert.That(blocked.Baseline.Cause.Origins.Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 診断差を比較して別ファイルへ保存する_診断の前後と非0を残しbaselineを保持する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baseline = CreateReport();
        var current = CreateReport();
        current.Inputs[0].Cases[1] = current.Inputs[0].Cases[1] with
        {
            Outcome = CaseOutcome.Failed,
            ExpectedText = "expected diagnostic",
            Diagnostics = [new("invoke", "actual diagnostic")],
        };
        current = current with { Summary = current.Summarize() };
        var baselinePath = directory.Combine("baseline.json");
        var currentPath = directory.Combine("current.json");
        var outputPath = directory.Combine("comparison.json");
        ReportStore.Save(baseline, baselinePath);
        ReportStore.Save(current, currentPath);
        var original = await File.ReadAllTextAsync(baselinePath);

        // Act
        var comparison = BaselineComparer.CompareRun(
            ReportStore.ReadRunReport(baselinePath).Report,
            ReportStore.ReadRunReport(currentPath).Report
        );
        ReportStore.Save(comparison, outputPath);

        // Assert
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        var changed = document
            .RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(x => x.GetProperty("regression").GetBoolean());
        using (Assert.Multiple())
        {
            await Assert
                .That(changed.GetProperty("baseline").GetProperty("outcome").GetString())
                .IsEqualTo("passed");
            await Assert
                .That(changed.GetProperty("current").GetProperty("outcome").GetString())
                .IsEqualTo("failed");
            await Assert
                .That(changed.GetProperty("current").GetProperty("expected_text").GetString())
                .IsEqualTo("expected diagnostic");
            await Assert
                .That(
                    changed
                        .GetProperty("current")
                        .GetProperty("diagnostics")[0]
                        .GetProperty("message")
                        .GetString()
                )
                .IsEqualTo("actual diagnostic");
            await Assert.That(await File.ReadAllTextAsync(baselinePath)).IsEqualTo(original);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
            await Assert
                .That(CompletionPolicy.CompareRun(comparison, saved: false).ExitCode)
                .IsEqualTo(2);
        }
    }

    private static RunReport CreateReport()
    {
        return RunReportFixture.Create(
            RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed) with
            {
                Line = 5,
            },
            RunReportFixture.Case("a.wast", 1, CaseCategory.Assertion, CaseOutcome.Passed) with
            {
                Line = 5,
                ExpectedValues = [new("v128") { LaneType = "i32", Lanes = ["0", "0", "0", "0"] }],
                ActualValues =
                [
                    new("v128") { Low64 = "0000000000000000", High64 = "0000000000000000" },
                ],
                LastStage = CaseStage.Invoke,
                Diagnostics = [new("invoke", "元の診断")],
                Prints = [new("print_i32") { Arguments = [new("i32") { Bits = "00000000" }] }],
            },
            RunReportFixture.Case("b.wast", 0, CaseCategory.Action, CaseOutcome.Passed)
        );
    }
}
