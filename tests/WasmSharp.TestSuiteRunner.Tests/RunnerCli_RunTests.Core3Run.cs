using System.Text.Json;
using System.Text.Json.Nodes;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    [Test]
    public async Task Core3の素材を実行する_使用した仕様版と元入力のcommand順序で結果を保存する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fixture = await Core3ConversionFixture.CreateAsync(directory, "gc/sample.wast", "(module)\n");
        fixture.Generate("generated");
        var outputPath = directory.Combine("run.json");

        // Act
        var (exitCode, error) = fixture.Run(["run", "--manifest", directory.Combine("generated/manifest.json"), "--output", outputPath]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsNotEqualTo(2).Because(error);
            using var report = JsonDocument.Parse(await File.ReadAllBytesAsync(outputPath));
            await Assert.That(report.RootElement.GetProperty("spec_version").GetString()).IsEqualTo("Core3");
            var cases = report.RootElement.GetProperty("inputs")[0].GetProperty("cases").EnumerateArray().ToArray();
            await Assert
                .That(cases.Select(x => x.GetProperty("id").GetProperty("input_path").GetString()!).Distinct())
                .IsEquivalentTo(["gc/sample.wast"]);
            await Assert.That(cases.Select(x => x.GetProperty("id").GetProperty("command_index").GetInt32())).IsEquivalentTo([0, 1, 2, 3, 4, 5, 6]);
        }
    }

    [Test]
    public async Task Core3の固定条件と異なるmanifest_Core2へ読み替えず実行を拒否する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fixture = await Core3ConversionFixture.CreateAsync(directory, "gc/sample.wast", "(module)\n");
        fixture.Generate("generated");
        var manifestPath = directory.Combine("generated/manifest.json");
        var manifest = JsonNode.Parse(await File.ReadAllBytesAsync(manifestPath))!;
        var feature = manifest["profile"]!["features"]![0]!;
        feature["enabled"] = !feature["enabled"]!.GetValue<bool>();
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());
        var outputPath = directory.Combine("run.json");

        // Act
        var (exitCode, error) = fixture.Run(["run", "--manifest", manifestPath, "--output", outputPath]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(error).Contains("固定profilecore3");
            await Assert.That(File.Exists(outputPath)).IsFalse();
        }
    }

    [Test]
    public async Task 仕様版の異なる実行結果を比較する_条件差を報告しケースの回帰を判定しない()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baselinePath = directory.Combine("baseline.json");
        var currentPath = directory.Combine("current.json");
        var outputPath = directory.Combine("comparison.json");
        _reportStore.Save(RunReportFixture.Create(RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed)), baselinePath);
        _reportStore.Save(
            RunReportFixture.Create(RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Failed)) with
            {
                SpecVersion = WasmSpecVersion.Core3,
            },
            currentPath
        );

        // Act
        var result = Run(_profileLoader.Load(), "compare-run", "--baseline", baselinePath, "--current", currentPath, "--output", outputPath);

        // Assert
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(outputPath));
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Output).Contains("条件差: 1件");
            await Assert.That(result.Output).Contains("回帰: 0件");
            var difference = json.RootElement.GetProperty("condition_differences")[0];
            await Assert.That(difference.GetProperty("name").GetString()).IsEqualTo("spec_version");
            await Assert.That(difference.GetProperty("baseline").GetString()).IsEqualTo("Core2");
            await Assert.That(difference.GetProperty("current").GetString()).IsEqualTo("Core3");
            await Assert.That(json.RootElement.GetProperty("cases").GetArrayLength()).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 仕様版を記録していない従来の結果と比較する_Core2として比較を成立させる()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baselinePath = directory.Combine("baseline.json");
        var currentPath = directory.Combine("current.json");
        var outputPath = directory.Combine("comparison.json");
        var report = RunReportFixture.Create(RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed));
        _reportStore.Save(report, currentPath);
        var legacy = JsonNode.Parse(await File.ReadAllBytesAsync(currentPath))!.AsObject();
        legacy.Remove("spec_version");
        await File.WriteAllTextAsync(baselinePath, legacy.ToJsonString());

        // Act
        var result = Run(_profileLoader.Load(), "compare-run", "--baseline", baselinePath, "--current", currentPath, "--output", outputPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Error);
            await Assert.That(result.Output).Contains("比較成立: 成立");
            await Assert.That(result.Output).Contains("条件差: 0件");
        }
    }
}
