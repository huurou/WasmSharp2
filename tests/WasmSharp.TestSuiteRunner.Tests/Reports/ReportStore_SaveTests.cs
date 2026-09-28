using System.Text.Json;
using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_SaveTests
{
    [Test]
    public async Task 実行結果とmanifestを保存する_schemaとkindを持つsnake_caseのUTF8JSONとして読み戻せる()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        var reportPath = directory.Combine("run.json");
        var manifestPath = directory.Combine("manifest.json");

        // Act
        ReportStore.Save(report, reportPath);
        ReportStore.Save(report.Corpus, manifestPath);

        // Assert
        var content = await File.ReadAllBytesAsync(reportPath);
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var cases = root.GetProperty("inputs")[0].GetProperty("cases");
        var stored = ReportStore.ReadRunReport(reportPath);
        using (Assert.Multiple())
        {
            await Assert.That(content[0]).IsEqualTo((byte)'{');
            await Assert.That(root.GetProperty("schema_version").GetInt32()).IsEqualTo(1);
            await Assert.That(root.GetProperty("kind").GetString()).IsEqualTo("run_report");
            await Assert
                .That(root.GetProperty("inputs")[0].GetProperty("status").GetString())
                .IsEqualTo("processed");
            await Assert
                .That(cases[1].GetProperty("id").GetProperty("command_index").GetInt32())
                .IsEqualTo(1);
            await Assert.That(cases[1].GetProperty("line").GetInt32()).IsEqualTo(2);
            await Assert.That(cases[2].GetProperty("line").GetInt32()).IsEqualTo(2);
            await Assert.That(cases[2].GetProperty("outcome").GetString()).IsEqualTo("failed");
            await Assert
                .That(cases[3].GetProperty("category").ValueKind)
                .IsEqualTo(JsonValueKind.Null);
            await Assert
                .That(cases[3].GetProperty("outcome").GetString())
                .IsEqualTo("runner_error");
            await Assert.That(stored.Issues).IsEmpty();
            await Assert
                .That(JsonSerializer.Serialize(stored.Report))
                .IsEqualTo(JsonSerializer.Serialize(report));
            await Assert.That(ReportStore.ReadManifest(manifestPath).Issues).IsEmpty();
            await Assert.That(Directory.GetFiles(directory.Root, "*.tmp")).IsEmpty();
        }
    }

    [Test]
    public async Task 比較結果を保存する_comparison_reportとして前後の詳細と回帰を保持する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baseline = RunReportFixture.Case(
            "a.wast",
            2,
            CaseCategory.Assertion,
            CaseOutcome.Passed
        );
        var report = new ComparisonReport(
            ComparisonType.Run,
            new ComparedReport("/baseline.json") { RunId = "run-1" },
            new ComparedReport("/current.json") { RunId = "run-2" }
        )
        {
            Established = true,
            Complete = true,
            Cases =
            [
                new(baseline.Id, CaseChange.Changed, Regression: true)
                {
                    Baseline = baseline,
                    Current = baseline with { Outcome = CaseOutcome.Failed },
                },
            ],
        };
        var path = directory.Combine("comparison.json");

        // Act
        ReportStore.Save(report, path);

        // Assert
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        var root = document.RootElement;
        var item = root.GetProperty("cases")[0];
        using (Assert.Multiple())
        {
            await Assert.That(root.GetProperty("schema_version").GetInt32()).IsEqualTo(1);
            await Assert.That(root.GetProperty("kind").GetString()).IsEqualTo("comparison_report");
            await Assert.That(root.GetProperty("comparison").GetString()).IsEqualTo("run");
            await Assert
                .That(root.GetProperty("current").GetProperty("run_id").GetString())
                .IsEqualTo("run-2");
            await Assert.That(item.GetProperty("change").GetString()).IsEqualTo("changed");
            await Assert.That(item.GetProperty("regression").GetBoolean()).IsTrue();
            await Assert
                .That(item.GetProperty("baseline").GetProperty("outcome").GetString())
                .IsEqualTo("passed");
            await Assert
                .That(item.GetProperty("current").GetProperty("outcome").GetString())
                .IsEqualTo("failed");
        }
    }

    [Test]
    [Arguments("corpus_manifest")]
    [Arguments("run_report")]
    [Arguments("comparison_report")]
    public async Task 保存先に既存ファイルがある_上書きせず既存内容を保持して保存先と理由を報告する(
        string kind
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("result.json");
        await File.WriteAllTextAsync(path, "既存の結果");
        var report = RunReportFixture.CreateSample();
        Action save = kind switch
        {
            "corpus_manifest" => () => ReportStore.Save(report.Corpus, path),
            "run_report" => () => ReportStore.Save(report, path),
            _ => () =>
                ReportStore.Save(
                    new ComparisonReport(ComparisonType.Run, new("/baseline.json"), new(path)),
                    path
                ),
        };

        // Act & Assert
        var exception = await Assert.That(save).ThrowsExactly<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(path);
            await Assert.That(exception.Message).Contains(exception.InnerException!.Message);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("既存の結果");
            await Assert.That(Directory.GetFiles(directory.Root, "*.tmp")).IsEmpty();
        }
    }

    [Test]
    public async Task 保存先の親ディレクトリがない_作成せず保存先と理由を報告する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var parent = directory.Combine("未作成");
        var path = Path.Combine(parent, "run.json");

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Save(RunReportFixture.CreateSample(), path))
            .ThrowsExactly<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(path);
            await Assert.That(exception.Path).IsEqualTo(path);
            await Assert.That(Directory.Exists(parent)).IsFalse();
        }
    }
}
