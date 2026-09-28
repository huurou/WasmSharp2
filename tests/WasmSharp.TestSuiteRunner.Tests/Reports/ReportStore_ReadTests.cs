using System.Text.Json;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_ReadTests
{
    [Test]
    public async Task 保存済みmanifestと実行結果を読む_種類に応じた内容と元のバイト列を返す()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        var manifestPath = await directory.WriteJsonAsync("manifest.json", report.Corpus);
        var reportPath = await directory.WriteJsonAsync("run.json", report);

        // Act
        var manifest = ReportStore.Read(manifestPath);
        var run = ReportStore.Read(reportPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest).IsTypeOf<StoredManifest>();
            await Assert.That(manifest.Issues).IsEmpty();
            await Assert
                .That(Convert.ToHexString(manifest.Content))
                .IsEqualTo(Convert.ToHexString(await File.ReadAllBytesAsync(manifestPath)));
            await Assert.That(run).IsTypeOf<StoredRunReport>();
            await Assert.That(run.Issues).IsEmpty();
            await Assert
                .That(JsonSerializer.Serialize(((StoredRunReport)run).Report))
                .IsEqualTo(JsonSerializer.Serialize(report));
        }
    }

    [Test]
    [Arguments(
        """
            "schema_version":2,"kind":"run_report"
            """,
        "schema_version"
    )]
    [Arguments(
        """
            "kind":"run_report"
            """,
        "schema_version"
    )]
    [Arguments(
        """
            "schema_version":1,"kind":"comparison_report"
            """,
        "kind"
    )]
    public async Task 有効な実行結果のschemaやkindだけを変える_読み替えず読取を拒否する(
        string header,
        string expected
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = await WriteModifiedSampleAsync(
            directory,
            """
            "schema_version":1,"kind":"run_report"
            """,
            header
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Read(path))
            .ThrowsExactly<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(path);
            await Assert.That(exception.Message).Contains(expected);
        }
    }

    [Test]
    [Arguments(
        """
            "kind":"run_report"
            """,
        """
            "kind":"run_report","kind":"run_report"
            """
    )]
    [Arguments(
        """
            "outcome":"failed"
            """,
        """
            "outcome":"failed","outcome":"passed"
            """
    )]
    [Arguments(
        """
            "outcome":"failed"
            """,
        """
            "outcome":1
            """
    )]
    [Arguments(
        """
            "outcome":"failed"
            """,
        """
            "outcome":"runtime_unsupported, out_of_scope"
            """
    )]
    [Arguments(
        """
            "outcome":"failed"
            """,
        """
            "outcome":"passed,failed"
            """
    )]
    public async Task 有効な実行結果に重複キーや数値と複合表記の分類を含める_構造不正として読取を拒否する(
        string original,
        string replacement
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = await WriteModifiedSampleAsync(directory, original, replacement);

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Read(path))
            .ThrowsExactly<ReportStoreException>();
        await Assert.That(exception!.InnerException).IsTypeOf<JsonException>();
    }

    [Test]
    [Arguments(
        """
            "cases":[
            """
    )]
    [Arguments(
        """
            "inputs":[
            """
    )]
    public async Task 有効な実行結果の一覧にnullの要素を含める_構造不正として読取を拒否する(
        string original
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = await WriteModifiedSampleAsync(directory, original, original + "null,");

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Read(path))
            .ThrowsExactly<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(path);
            await Assert.That(exception.InnerException).IsTypeOf<JsonException>();
        }
    }

    [Test]
    public async Task 途中まで書かれた実行結果を読む_構文破損として読取を拒否する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = await directory.WriteJsonAsync("run.json", RunReportFixture.CreateSample());
        var content = await File.ReadAllBytesAsync(path);
        await File.WriteAllBytesAsync(path, content[..(content.Length / 2)]);

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Read(path))
            .ThrowsExactly<ReportStoreException>();
        await Assert.That(exception!.InnerException).IsTypeOf<JsonException>();
    }

    [Test]
    public async Task 素材スナップショットと集計を欠く標本を読む_構造不正として読取を拒否する()
    {
        // Arrange
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Data",
            "incomplete-run-report.json"
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Read(path))
            .ThrowsExactly<ReportStoreException>();
        await Assert.That(exception!.InnerException).IsTypeOf<JsonException>();
    }

    [Test]
    [Arguments("a.wast", -1)]
    [Arguments("b.wast", 0)]
    public async Task ケース識別を読めない_読取を拒否する(string inputPath, int commandIndex)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        report.Inputs[0].Cases[0] = report.Inputs[0].Cases[0] with
        {
            Id = new(inputPath, commandIndex),
        };
        var path = await directory.WriteJsonAsync("run.json", report);

        // Act & Assert
        var exception = await Assert
            .That(() => ReportStore.Read(path))
            .ThrowsExactly<ReportStoreException>();
        await Assert.That(exception!.Message).Contains($"{inputPath}#{commandIndex}");
    }

    [Test]
    public async Task 欠落と重複と件数未確定を含む実行結果を読む_拒否せず対応可能なケースと診断を返す()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        var cases = report.Inputs[0].Cases;
        report.Inputs[0] = report.Inputs[0] with
        {
            CommandCount = null,
            Cases = [cases[0], cases[2], cases[2], cases[3]],
        };
        var path = await directory.WriteJsonAsync("run.json", report);

        // Act
        var stored = (StoredRunReport)ReportStore.Read(path);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stored.Report.Inputs[0].Cases.Count).IsEqualTo(4);
            await Assert.That(stored.Report.Inputs[1].Cases.Count).IsEqualTo(2);
            await Assert
                .That(
                    stored.Issues.Any(x =>
                        x.Kind == RecordIssueKind.Missing && x.Message.Contains("a.wast#1")
                    )
                )
                .IsTrue();
            await Assert
                .That(
                    stored.Issues.Any(x =>
                        x.Kind == RecordIssueKind.Duplicate && x.Message.Contains("a.wast#2")
                    )
                )
                .IsTrue();
            await Assert
                .That(stored.Issues.Any(x => x.Kind == RecordIssueKind.Undetermined))
                .IsTrue();
            await Assert
                .That(stored.Issues.Any(x => x.Kind == RecordIssueKind.Inconsistent))
                .IsTrue();
        }
    }

    private static async Task<string> WriteModifiedSampleAsync(
        TemporaryDirectory directory,
        string original,
        string replacement
    )
    {
        var path = await directory.WriteJsonAsync("run.json", RunReportFixture.CreateSample());
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(
            path,
            json.Replace(original, replacement, StringComparison.Ordinal)
        );
        return path;
    }
}
