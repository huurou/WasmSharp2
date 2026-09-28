using System.Text.Json;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class RunReport_CreateTests
{
    [Test]
    public async Task 部分生成のmanifestから作る_全対象入力を未処理で保持し素材と実行条件を記録する()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create();
        var provenance = new RunProvenance("run-1")
        {
            StartedAt = DateTimeOffset.Parse("2026-09-28T00:00:00Z"),
            ManifestPath = "/output/manifest.json",
        };
        var executionPolicy = new RunExecutionPolicy(1024);
        OutcomeCounts zero = new(0, 0, 0, 0, 0, 0);

        // Act
        var report = RunReport.Create(manifest, provenance, executionPolicy);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.SchemaVersion).IsEqualTo(1);
            await Assert.That(report.Kind).IsEqualTo("run_report");
            await Assert
                .That(
                    report
                        .Inputs.Select(x => x.InputPath)
                        .SequenceEqual(["failed.wast", "success.wast", "unprocessed.wast"])
                )
                .IsTrue();
            await Assert
                .That(
                    report.Inputs.All(x =>
                        x.Status == InputRunStatus.Unprocessed
                        && x.CommandCount is null
                        && x.EnumeratedCount == 0
                        && x.UnprocessedCount == 0
                        && x.Cases.Count == 0
                        && x.Issues.Count == 0
                    )
                )
                .IsTrue();
            await Assert
                .That(report.Summary)
                .IsEqualTo(new RunSummary(3, 0, 0, 3, 0, 0, 0, 0, 0, zero, zero, zero, 0));
            await Assert.That(report.Completion).IsEqualTo(new RunCompletion(false, false));
            await Assert.That(report.Provenance).IsEqualTo(provenance);
            await Assert.That(report.ExecutionPolicy).IsEqualTo(executionPolicy);
            await Assert
                .That(JsonSerializer.Serialize(report.Corpus))
                .IsEqualTo(JsonSerializer.Serialize(manifest));
        }
    }

    [Test]
    public async Task 元manifestの入れ子の一覧を変更する_実行結果の素材スナップショットは変化しない()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create();
        var report = RunReport.Create(manifest, new RunProvenance("run-1"), new(1024));
        var before = JsonSerializer.Serialize(report.Corpus);

        // Act
        manifest.Profile.Inputs.Clear();
        manifest.Inputs[1].Artifacts[0].Script!.Commands.Clear();
        manifest.Inputs[1].Artifacts.Clear();
        manifest.Inputs.Clear();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(JsonSerializer.Serialize(report.Corpus)).IsEqualTo(before);
            await Assert.That(report.Inputs.Count).IsEqualTo(3);
        }
    }
}
