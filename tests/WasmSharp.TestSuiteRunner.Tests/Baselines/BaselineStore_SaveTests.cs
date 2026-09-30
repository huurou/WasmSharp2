using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Baselines;

internal class BaselineStore_SaveTests
{
    [Test]
    public async Task 全件記録済みでfailedとrunner_errorと未対応を含む_バイト列を変えず既存baselineへ保存する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var inputPath = directory.Combine("input.json");
        var outputPath = directory.Combine("baseline.json");
        ReportStore.Save(RunReportFixture.CreateSample(), inputPath);
        await File.AppendAllTextAsync(inputPath, "\n  \n");
        await File.WriteAllTextAsync(outputPath, "旧baseline");
        var original = await File.ReadAllBytesAsync(inputPath);

        // Act
        BaselineStore.Save(inputPath, outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(Convert.ToHexString(await File.ReadAllBytesAsync(outputPath)))
                .IsEqualTo(Convert.ToHexString(original));
            await Assert
                .That(Convert.ToHexString(await File.ReadAllBytesAsync(inputPath)))
                .IsEqualTo(Convert.ToHexString(original));
            await Assert.That(ReportStore.ReadRunReport(outputPath).Issues).IsEmpty();
            await Assert.That(Directory.GetFiles(directory.Root, "*.tmp")).IsEmpty();
        }
    }

    [Test]
    public async Task 全入力の変換試行が完了しrunner_errorを含む_変換状態と内容を変えず保存する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var manifest = RunReportFixture.CreateManifest(("a.wast", 1));
        manifest.Inputs[0] = manifest.Inputs[0] with
        {
            Status = ConversionStatus.RunnerError,
            ExitCode = 1,
            Artifacts = [],
            Diagnostics = [new("convert", "変換失敗", "a.wast")],
        };
        manifest = manifest with { Summary = manifest.Summarize() };
        var inputPath = directory.Combine("input.json");
        var outputPath = directory.Combine("baseline.json");
        ReportStore.Save(manifest, inputPath);

        // Act
        BaselineStore.Save(inputPath, outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(Convert.ToHexString(await File.ReadAllBytesAsync(outputPath)))
                .IsEqualTo(Convert.ToHexString(await File.ReadAllBytesAsync(inputPath)));
            await Assert
                .That(ReportStore.ReadManifest(outputPath).Manifest.Inputs[0].Status)
                .IsEqualTo(ConversionStatus.RunnerError);
        }
    }

    [Test]
    [Arguments("未処理")]
    [Arguments("未確定")]
    [Arguments("出力失敗")]
    [Arguments("ケース欠落")]
    [Arguments("ケース重複")]
    [Arguments("集計不整合")]
    public async Task 実行結果の記録が未完了_理由を報告し既存baselineを保持する(string issue)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        switch (issue)
        {
            case "未処理":
                report.Inputs[0] = report.Inputs[0] with { Status = InputRunStatus.Unprocessed };
                break;
            case "未確定":
                report.Inputs[0] = report.Inputs[0] with { CommandCount = null };
                break;
            case "出力失敗":
                report = report with { Completion = new(true, false) };
                break;
            case "ケース欠落":
                report.Inputs[0].Cases.RemoveAt(0);
                break;
            case "ケース重複":
                report.Inputs[0].Cases.Add(report.Inputs[0].Cases[0]);
                break;
            case "集計不整合":
                report = report with { Summary = RunSummary.Empty };
                break;
        }

        if (issue != "集計不整合")
        {
            report = report with { Summary = report.Summarize() };
        }

        var inputPath = directory.Combine("input.json");
        var outputPath = directory.Combine("baseline.json");
        ReportStore.Save(report, inputPath);
        await File.WriteAllTextAsync(outputPath, "旧baseline");

        // Act & Assert
        var exception = await Assert
            .That(() => BaselineStore.Save(inputPath, outputPath))
            .Throws<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(inputPath);
            await Assert.That(exception.Message).Contains("baseline");
            await Assert.That(await File.ReadAllTextAsync(outputPath)).IsEqualTo("旧baseline");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Manifestに未処理または出力失敗が残る_既存baselineを保持する(
        bool outputFailure
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var manifest = RunReportFixture.CreateManifest(("a.wast", 1));
        if (outputFailure)
        {
            manifest = manifest with { Completion = new(true, false) };
        }
        else
        {
            manifest.Inputs[0] = manifest.Inputs[0] with { Status = ConversionStatus.Unprocessed };
            manifest = manifest with { Summary = manifest.Summarize() };
        }

        var inputPath = directory.Combine("input.json");
        var outputPath = directory.Combine("baseline.json");
        ReportStore.Save(manifest, inputPath);
        await File.WriteAllTextAsync(outputPath, "旧baseline");

        // Act & Assert
        await Assert
            .That(() => BaselineStore.Save(inputPath, outputPath))
            .Throws<ReportStoreException>();
        await Assert.That(await File.ReadAllTextAsync(outputPath)).IsEqualTo("旧baseline");
    }

    [Test]
    public async Task 入力JSONを読み取れない_既存baselineを保持する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var inputPath = directory.Combine("input.json");
        var outputPath = directory.Combine("baseline.json");
        await File.WriteAllTextAsync(inputPath, "{破損");
        await File.WriteAllTextAsync(outputPath, "旧baseline");

        // Act & Assert
        await Assert
            .That(() => BaselineStore.Save(inputPath, outputPath))
            .Throws<ReportStoreException>();
        await Assert.That(await File.ReadAllTextAsync(outputPath)).IsEqualTo("旧baseline");
    }

    [Test]
    public async Task 保存先がディレクトリで確定保存に失敗する_入力と既存内容を保持して理由を報告する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var inputPath = directory.Combine("input.json");
        var outputPath = directory.Combine("baseline.json");
        ReportStore.Save(RunReportFixture.CreateSample(), inputPath);
        Directory.CreateDirectory(outputPath);
        var markerPath = Path.Combine(outputPath, "existing.txt");
        await File.WriteAllTextAsync(markerPath, "既存内容");
        var original = await File.ReadAllBytesAsync(inputPath);

        // Act & Assert
        var exception = await Assert
            .That(() => BaselineStore.Save(inputPath, outputPath))
            .Throws<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(outputPath);
            await Assert.That(await File.ReadAllTextAsync(markerPath)).IsEqualTo("既存内容");
            await Assert
                .That(Convert.ToHexString(await File.ReadAllBytesAsync(inputPath)))
                .IsEqualTo(Convert.ToHexString(original));
            await Assert.That(Directory.GetFiles(directory.Root, "*.tmp")).IsEmpty();
        }
    }

    [Test]
    public async Task 入力自身を出力先に指定する_入力を保持して拒否する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("input.json");
        ReportStore.Save(RunReportFixture.CreateSample(), path);
        var original = await File.ReadAllBytesAsync(path);

        // Act & Assert
        await Assert
            .That(() => BaselineStore.Save(path, directory.Combine("./input.json")))
            .Throws<ReportStoreException>();
        await Assert
            .That(Convert.ToHexString(await File.ReadAllBytesAsync(path)))
            .IsEqualTo(Convert.ToHexString(original));
    }
}
