using System.Text;
using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    private readonly ReportStore _reportStore = new ReportStore();

    private readonly ConversionProfileLoader _profileLoader = new ConversionProfileLoader();

    [Test]
    [Arguments("", "有効な操作")]
    [Arguments("unknown", "有効な操作")]
    [Arguments("unknown --help", "有効な操作")]
    [Arguments("run --manifest a", "必須引数が不足しています: --output")]
    [Arguments("run --manifest a --output b --output c", "引数が重複しています: --output")]
    [Arguments("run --manifest a --output b --select c", "未知の引数です: --select")]
    [Arguments("verify --input", "--inputのpathが不足")]
    [Arguments("verify --input a --output b", "未知の引数です: --output")]
    [Arguments("verify --input a --profile b", "未知の引数です: --profile")]
    public async Task 未知か重複か不足の引数_使用法を標準エラーへ出して終了2になる(string arguments, string reason)
    {
        // Arrange
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Act
        var result = Run(_profileLoader.Load(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("使用法");
            await Assert.That(result.Error).Contains(reason);
            await Assert.That(result.Output).IsEqualTo(string.Empty);
        }
    }

    [Test]
    [Arguments("--help")]
    [Arguments("generate --help")]
    [Arguments("run --help")]
    public async Task ヘルプ_6操作と初回と後続の使用例を表示して終了0になる(string arguments)
    {
        // Arrange
        var args = arguments.Split(' ');

        // Act
        var result = Run(_profileLoader.Load(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            foreach (
                var operation in new[] { "generate", "run", "baseline-save", "compare-conversion", "compare-run", "verify" }
            )
            {
                await Assert.That(result.Output).Contains(operation);
            }
            await Assert.That(result.Output).Contains("初回");
            await Assert.That(result.Output).Contains("後続");
            await Assert
                .That(result.Output)
                .Contains(
                    OperatingSystem.IsWindows()
                        ? "--wast2json artifacts/wabt-core2-1.0.42-datacount/Release/wast2json.exe"
                        : "--wast2json artifacts/wabt-core2-1.0.42-datacount/wast2json"
                );
            await Assert.That(result.Error).IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task 変換失敗と部分出力_独立入力を継続しmanifestだけを保存して終了1になる()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();

        // Act
        var result = Run(profile, GenerateArguments(workspace, workspace.OutputRoot));
        var manifest = _reportStore.ReadManifest(Path.Combine(workspace.OutputRoot, "manifest.json")).Manifest;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(manifest.Summary.SucceededCount).IsEqualTo(1);
            await Assert.That(manifest.Summary.RunnerErrorCount).IsEqualTo(3);
            await Assert.That(manifest.Completion.OutputComplete).IsTrue();
            await Assert.That(result.Output).Contains("対象入力: 4件");
            await Assert.That(result.Output).Contains("変換成功: 1件");
            await Assert.That(result.Output).Contains("入力異常: 3件");
            await Assert.That(result.Output).Contains("処理済み: 4件、未処理: 0件");
            await Assert.That(result.Output).Contains("command件数未確定: 1入力");
            await Assert.That(result.Output).Contains("生成判定: 条件不成立（終了値1）");
            await Assert.That(result.Output).Contains("変換・照合に失敗した入力が3件");
            await Assert.That(result.Error).DoesNotContain("変換・照合に失敗した入力が3件");
            await Assert.That(result.Error).Contains("failure.wast:");
            await Assert.That(result.Error).Contains("変換");
            await Assert.That(result.Output).Contains("[4/4]");
            await Assert.That(result.Output).Contains("保存先:");
            await Assert.That(Directory.GetFiles(workspace.OutputRoot, "*.json").Length).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 相対指定の生成先_CLI開始位置で解決して終了0になる()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        foreach (
            var path in Directory.GetFiles(workspace.InputRoot, "*.wast").Where(x => Path.GetFileName(x) != "success.wast")
        )
        {
            File.Delete(path);
        }
        var profile = workspace.CreateProfile();
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, workspace.OutputRoot);

        // Act
        var result = Run(profile, GenerateArguments(workspace, relative));
        var manifest = _reportStore.ReadManifest(Path.Combine(workspace.OutputRoot, "manifest.json")).Manifest;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(manifest.Provenance.OutputRoot).IsEqualTo(workspace.OutputRoot);
            await Assert
                .That(manifest.Inputs[0].Arguments[2])
                .IsEqualTo(Path.Combine(workspace.OutputRoot, "modules", "success.json"));
            await Assert.That(result.Error).IsEqualTo(string.Empty);
            await Assert.That(result.Output).Contains("生成判定: 成立（終了値0）");
            await Assert.That(result.Output).Contains("command件数未確定: 0入力");
        }
    }

    [Test]
    [Arguments("child")]
    [Arguments("same")]
    [Arguments("ancestor")]
    [Arguments("nonempty")]
    public async Task ソース配下か祖先か空でない生成先_既存内容を保護して終了2になる(string placement)
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var output = placement switch
        {
            "child" => Path.Combine(workspace.SourceRoot, "generated"),
            "same" => workspace.SourceRoot,
            "ancestor" => Path.GetDirectoryName(workspace.SourceRoot)!,
            _ => workspace.OutputRoot,
        };
        var marker = Path.Combine(workspace.OutputRoot, "marker.txt");
        File.WriteAllText(marker, "保持する内容");
        var original = File.ReadAllBytes(Path.Combine(workspace.InputRoot, "success.wast"));

        // Act
        var result = Run(workspace.CreateProfile(), GenerateArguments(workspace, output));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).IsNotEqualTo(string.Empty);
            if (placement == "nonempty")
            {
                await Assert.That(result.Error).Contains(output);
                await Assert.That(result.Error).Contains("既存");
            }
            await Assert.That(File.ReadAllText(marker)).IsEqualTo("保持する内容");
            await Assert.That(File.ReadAllBytes(Path.Combine(workspace.InputRoot, "success.wast"))).IsEquivalentTo(original);
            await Assert.That(File.Exists(Path.Combine(output, "manifest.json"))).IsFalse();
        }
    }

    [Test]
    [Arguments("passed", 0)]
    [Arguments("diagnostic", 1)]
    [Arguments("broken", 1)]
    [Arguments("operation", 1)]
    public async Task 保存済み素材の実行_元ソースを使わず分類と集計を保存する(string scenario, int expectedExit)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        PrepareScript(fixture, scenario);
        var manifest = Complete(fixture.Manifest);
        if (scenario == "operation")
        {
            manifest.Diagnostics.Add(new("generate", "生成時に記録された操作全体の異常"));
        }
        _reportStore.Save(manifest, fixture.ManifestPath);
        Directory.Delete(fixture.SourceRoot, recursive: true);
        var outputPath = Path.Combine(fixture.Root, "run.json");

        // Act
        var result = Run(manifest.Profile.ToProfile(), "run", "--manifest", fixture.ManifestPath, "--output", outputPath);
        var report = _reportStore.ReadRunReport(outputPath).Report;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(expectedExit);
            await Assert.That(report.Summary.ProcessedInputCount).IsEqualTo(2);
            await Assert.That(result.Output).Contains("対象入力: 2件");
            await Assert.That(result.Output).Contains($"列挙済みcommand: {report.Summary.EnumeratedCommandCount}件");
            await Assert.That(result.Output).Contains($"入力異常: {report.Summary.InputIssueCount}件");
            await Assert.That(result.Output).Contains($"セットアップ: passed={report.Summary.Setup.Passed}");
            await Assert.That(result.Output).Contains("単独action:");
            await Assert.That(result.Output).Contains("assertion:");
            await Assert.That(result.Output).Contains("種類未確定command:");
            await Assert.That(result.Output).Contains("[2/2]");
            await Assert
                .That(result.Output)
                .Contains($"実行判定: {(expectedExit == 0 ? "成立" : "条件不成立")}（終了値{expectedExit}）");
            if (scenario == "diagnostic")
            {
                await Assert.That(report.Summary.Assertion.Failed).IsEqualTo(1);
                await Assert.That(report.Inputs[0].Cases[0].ExpectedText).IsEqualTo("一致しない診断");
            }
            if (scenario == "broken")
            {
                await Assert.That(report.Summary.UndeterminedInputCount).IsEqualTo(1);
                await Assert.That(result.Output).Contains("件数未確定: 1件");
            }
            if (scenario == "operation")
            {
                await Assert.That(result.Error).Contains("生成時に記録された操作全体の異常");
                await Assert.That(result.Output).Contains("操作全体の診断が1件");
            }
        }
    }

    [Test]
    [Arguments("run")]
    [Arguments("verify")]
    public async Task 埋込みprofileと異なる対象集合_対象を狭めず拒否する(string operation)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var manifest = Complete(fixture.Manifest);
        _reportStore.Save(manifest, fixture.ManifestPath);
        var outputPath = Path.Combine(fixture.Root, "run.json");
        var reportPath = Path.Combine(fixture.Root, "existing-run.json");
        _reportStore.Save(RunReportFixture.CreateSample(), reportPath);
        string[] args =
            operation == "run"
                ? ["run", "--manifest", fixture.ManifestPath, "--output", outputPath]
                : ["verify", "--input", reportPath];

        // Act
        var result = Run(_profileLoader.Load(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(operation == "verify" ? 1 : 2);
            await Assert.That(File.Exists(outputPath)).IsFalse();
            await Assert.That(operation == "run" ? result.Error : result.Output).Contains("固定profile");
        }
    }

    [Test]
    public async Task 不一致を含む完了結果のbaseline保存_生バイトを保持して明示置換だけを行う()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample();
        var input = directory.Combine("input.json");
        var baseline = directory.Combine("baseline.json");
        _reportStore.Save(report, input);
        File.WriteAllText(baseline, "古いbaseline");

        // Act
        var result = Run(_profileLoader.Load(), "baseline-save", "--input", input, "--output", baseline);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(File.ReadAllBytes(baseline)).IsEquivalentTo(File.ReadAllBytes(input));
            await Assert.That(result.Output).Contains("baseline保存");
            await Assert.That(result.Output).Contains($"保存先: {baseline}");
            await Assert.That(result.Error).IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task 未完了結果のbaseline保存_既存baselineを保持して終了2になる()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.CreateSample() with { Completion = new(false, true) };
        var input = directory.Combine("incomplete.json");
        var baseline = directory.Combine("baseline.json");
        _reportStore.Save(report, input);
        File.WriteAllText(baseline, "旧baseline");

        // Act
        var result = Run(_profileLoader.Load(), "baseline-save", "--input", input, "--output", baseline);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(File.ReadAllText(baseline)).IsEqualTo("旧baseline");
            await Assert.That(result.Error).Contains("保存できません");
        }
    }

    [Test]
    [Arguments("compare-run", "same", 0)]
    [Arguments("compare-run", "failed", 1)]
    [Arguments("compare-run", "regression", 1)]
    [Arguments("compare-run", "missing", 2)]
    [Arguments("compare-run", "condition", 2)]
    [Arguments("compare-conversion", "same", 0)]
    [Arguments("compare-conversion", "condition", 1)]
    [Arguments("compare-conversion", "incomplete", 2)]
    public async Task 保存済み結果の比較_操作別の終了値と詳細差分を保存しbaselineを変更しない(
        string operation,
        string scenario,
        int expectedExit
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baselinePath = directory.Combine("baseline.json");
        var currentPath = directory.Combine("current.json");
        var outputPath = directory.Combine("comparison.json");
        var baseline = RunReportFixture.Create(
            RunReportFixture.Case(
                "a.wast",
                0,
                CaseCategory.Assertion,
                scenario == "failed" ? CaseOutcome.Failed : CaseOutcome.Passed
            )
        );
        var current = RunReportFixture.Create(
            RunReportFixture.Case(
                "a.wast",
                0,
                CaseCategory.Assertion,
                scenario is "failed" or "regression" ? CaseOutcome.Failed : CaseOutcome.Passed
            )
        );
        if (scenario == "condition")
        {
            current = current with
            {
                Corpus = current.Corpus with { Profile = current.Corpus.Profile with { Id = "変更後" } },
            };
        }
        if (scenario == "missing")
        {
            current.Inputs[0].Cases.Clear();
            current = current with { Summary = current.Summarize() };
        }
        if (scenario == "incomplete")
        {
            current = current with { Corpus = current.Corpus with { Completion = new(false, true) } };
        }
        if (operation == "compare-run")
        {
            _reportStore.Save(baseline, baselinePath);
            _reportStore.Save(current, currentPath);
        }
        else
        {
            _reportStore.Save(baseline.Corpus, baselinePath);
            _reportStore.Save(current.Corpus, currentPath);
        }
        var original = File.ReadAllBytes(baselinePath);

        // Act
        var result = Run(
            _profileLoader.Load(),
            operation,
            "--baseline",
            baselinePath,
            "--current",
            currentPath,
            "--output",
            outputPath
        );
        using var json = JsonDocument.Parse(File.ReadAllBytes(outputPath));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(expectedExit);
            await Assert.That(json.RootElement.GetProperty("kind").GetString()).IsEqualTo("comparison_report");
            await Assert
                .That(json.RootElement.GetProperty("baseline").GetProperty("path").GetString())
                .IsEqualTo(baselinePath);
            await Assert
                .That(json.RootElement.GetProperty("current").GetProperty("path").GetString())
                .IsEqualTo(currentPath);
            await Assert.That(File.ReadAllBytes(baselinePath)).IsEquivalentTo(original);
            await Assert.That(result.Output).Contains("保存先:");
            var established = operation != "compare-run" || scenario != "condition";
            await Assert.That(result.Output).Contains($"比較成立: {(established ? "成立" : "未成立")}");
            await Assert.That(result.Output).Contains($"比較完了: {(expectedExit == 2 ? "未完了" : "完了")}");
            await Assert
                .That(result.Output)
                .Contains($"条件差: {(scenario == "condition" ? 1 : 0)}件、出典差: 0件、入力・素材差: 0件");
            await Assert
                .That(result.Output)
                .Contains($"現結果のfailed: {(scenario is "failed" or "regression" ? 1 : 0)}件、現結果のrunner_error: 0件");
            await Assert
                .That(result.Output)
                .Contains(
                    $"比較判定: {(expectedExit == 0 ? "成立" : expectedExit == 1 ? "条件不成立" : "未完了")}（終了値{expectedExit}）"
                );
            if (operation == "compare-run" && scenario == "condition")
            {
                await Assert.That(result.Error).Contains("比較成立の条件が一致しない");
            }
            if (scenario == "regression")
            {
                await Assert
                    .That(json.RootElement.GetProperty("summary").GetProperty("regression_count").GetInt32())
                    .IsEqualTo(1);
                await Assert.That(result.Output).Contains("回帰: 1件");
            }
        }
    }

    [Test]
    [Arguments("passed", 0)]
    [Arguments("unsupported", 1)]
    [Arguments("incomplete", 1)]
    [Arguments("profile", 1)]
    public async Task 単一結果の最終判定_再実行せず不成立の理由を表示する(string scenario, int expectedExit)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var report = RunReportFixture.Create(
            RunReportFixture.Case(
                "a.wast",
                0,
                CaseCategory.Setup,
                scenario == "unsupported" ? CaseOutcome.RuntimeUnsupported : CaseOutcome.Passed
            )
        );
        if (scenario == "incomplete")
        {
            report = report with { Completion = new(false, true) };
        }
        var profile = report.Corpus.Profile.ToProfile();
        if (scenario == "profile")
        {
            profile = _profileLoader.Load();
        }
        var input = directory.Combine("run.json");
        _reportStore.Save(report, input);
        var original = File.ReadAllBytes(input);

        // Act
        var result = Run(profile, "verify", "--input", input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(expectedExit);
            await Assert.That(File.ReadAllBytes(input)).IsEquivalentTo(original);
            await Assert.That(Directory.GetFiles(directory.Root).Length).IsEqualTo(1);
            await Assert.That(result.Output).Contains(expectedExit == 0 ? "最終判定: 合格" : "最終判定: 不合格");
            if (scenario == "unsupported")
            {
                await Assert.That(result.Output).Contains("runtime_unsupportedが1件");
            }
            await Assert.That(result.Error).IsEqualTo(string.Empty);
        }
    }

    [Test]
    [Arguments("run")]
    [Arguments("baseline-save")]
    [Arguments("compare-run")]
    [Arguments("compare-conversion")]
    public async Task 入力自身への出力_入力を保護して終了2になる(string operation)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var input = directory.Combine("input.json");
        _reportStore.Save(RunReportFixture.CreateSample(), input);
        var original = File.ReadAllBytes(input);
        var args = operation switch
        {
            "run" => new[] { operation, "--manifest", input, "--output", input },
            "baseline-save" => [operation, "--input", input, "--output", input],
            _ => [operation, "--baseline", input, "--current", input, "--output", input],
        };

        // Act
        var result = Run(_profileLoader.Load(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(File.ReadAllBytes(input)).IsEquivalentTo(original);
            await Assert.That(result.Error).Contains("入力自身");
        }
    }

    [Test]
    [Arguments("run")]
    [Arguments("compare-run")]
    [Arguments("compare-conversion")]
    public async Task 入力以外の既存出力_無関係なbaselineを上書きせず終了2になる(string operation)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var input = directory.Combine("input.json");
        var output = directory.Combine("baseline.json");
        _reportStore.Save(RunReportFixture.CreateSample(), input);
        File.WriteAllText(output, "保持するbaseline");
        var args =
            operation == "run"
                ? new[] { operation, "--manifest", input, "--output", output }
                : [operation, "--baseline", input, "--current", input, "--output", output];

        // Act
        var result = Run(_profileLoader.Load(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(File.ReadAllText(output)).IsEqualTo("保持するbaseline");
            await Assert.That(result.Error).Contains("既存");
        }
    }

    [Test]
    public async Task 保存先の親がファイル_保存先と理由を標準エラーへ出して終了2になる()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        PrepareScript(fixture, "passed");
        var manifest = Complete(fixture.Manifest);
        _reportStore.Save(manifest, fixture.ManifestPath);
        var parent = Path.Combine(fixture.Root, "file");
        File.WriteAllText(parent, "親はファイル");
        var output = Path.Combine(parent, "run.json");

        // Act
        var result = Run(manifest.Profile.ToProfile(), "run", "--manifest", fixture.ManifestPath, "--output", output);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains(output);
            await Assert.That(result.Output).DoesNotContain("保存先:");
            await Assert.That(File.ReadAllText(parent)).IsEqualTo("親はファイル");
        }
    }

    [Test]
    [Arguments("run")]
    [Arguments("generate")]
    public async Task 進捗通知中に出力先が作られる_競合ファイルを保持して終了2になる(string operation)
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        using var fixture = CorpusFixture.Create();
        PrepareScript(fixture, "passed");
        var manifest = Complete(fixture.Manifest);
        _reportStore.Save(manifest, fixture.ManifestPath);
        var outputPath =
            operation == "run"
                ? Path.Combine(fixture.Root, "run.json")
                : Path.Combine(workspace.OutputRoot, "manifest.json");
        var profile = operation == "run" ? manifest.Profile.ToProfile() : workspace.CreateProfile();
        var args =
            operation == "run"
                ? ["run", "--manifest", fixture.ManifestPath, "--output", outputPath]
                : GenerateArguments(workspace, workspace.OutputRoot);
        var beforeSave = false;
        using var output = new ProgressWriter(line =>
        {
            if (line?.StartsWith('[') == true && !File.Exists(outputPath))
            {
                beforeSave = true;
                File.WriteAllText(outputPath, "競合して作成された結果");
            }
        });
        using var error = new StringWriter();

        // Act
        var exitCode = new RunnerCli(profile).Run(args, output, error);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(beforeSave).IsTrue();
            await Assert.That(exitCode).IsEqualTo(2);
            await Assert.That(File.ReadAllText(outputPath)).IsEqualTo("競合して作成された結果");
            await Assert.That(error.ToString()).Contains(outputPath);
            await Assert.That(output.ToString()).DoesNotContain("保存先:");
        }
    }

    [Test]
    public async Task WABTだけのソース配下への生成_空の配置でも変換前に拒否する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        using var directory = new TemporaryDirectory();
        var output = directory.Combine("generated");
        Directory.CreateDirectory(output);
        var args = GenerateArguments(workspace, output);
        args[4] = directory.Root;

        // Act
        var result = Run(workspace.CreateProfile(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("--wabt-root");
            await Assert.That(Directory.EnumerateFileSystemEntries(output).Count()).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 固定入力のhashが不一致の生成_変換せず全未処理を保存して終了2になる()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();
        File.AppendAllText(Path.Combine(workspace.InputRoot, "success.wast"), "変更");

        // Act
        var result = Run(profile, GenerateArguments(workspace, workspace.OutputRoot));
        var manifest = _reportStore.ReadManifest(Path.Combine(workspace.OutputRoot, "manifest.json")).Manifest;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(manifest.Summary.UnprocessedCount).IsEqualTo(4);
            await Assert.That(manifest.Summary.ArtifactCount).IsEqualTo(0);
            await Assert.That(result.Error).IsNotEqualTo(string.Empty);
            await Assert.That(result.Output).Contains("未処理: 4件");
            await Assert.That(result.Output).Contains("command件数未確定: 0入力");
            await Assert.That(result.Output).Contains("生成判定: 未完了（終了値2）");
            await Assert.That(result.Error).Contains("SHA-256");
            await Assert.That(result.Error).Contains("操作全体の診断が1件");
            await Assert.That(result.Output).DoesNotContain("操作全体の診断が1件");
        }
    }

    [Test]
    [Arguments("run")]
    [Arguments("baseline-save")]
    [Arguments("compare-conversion")]
    [Arguments("compare-run")]
    [Arguments("verify")]
    public async Task 読み取れない保存済み入力_標準エラーへ理由を出して終了2になる(string operation)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var input = directory.Combine("missing.json");
        var output = directory.Combine("output.json");
        var args = operation switch
        {
            "run" => new[] { operation, "--manifest", input, "--output", output },
            "baseline-save" => [operation, "--input", input, "--output", output],
            "verify" => [operation, "--input", input],
            _ => [operation, "--baseline", input, "--current", input, "--output", output],
        };

        // Act
        var result = Run(_profileLoader.Load(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains(input);
            await Assert.That(File.Exists(output)).IsFalse();
        }
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("--output")]
    public async Task Path引数が空白か次のoption_不足した引数名を示して終了2になる(string path)
    {
        // Arrange
        var profile = _profileLoader.Load();

        // Act
        var result = Run(profile, "verify", "--input", path);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("--inputのpathが不足");
            await Assert.That(result.Error).Contains("使用法");
        }
    }

    [Test]
    [Arguments("--baseline")]
    [Arguments("--current")]
    public async Task 比較の片側を出力に指定する_入力自身の保護を理由に拒否する(string protectedOption)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baseline = directory.Combine("baseline.json");
        var current = directory.Combine("current.json");
        _reportStore.Save(RunReportFixture.CreateSample(), baseline);
        _reportStore.Save(RunReportFixture.CreateSample(), current);

        // Act
        var result = Run(
            _profileLoader.Load(),
            "compare-run",
            "--baseline",
            baseline,
            "--current",
            current,
            "--output",
            protectedOption == "--baseline" ? baseline : current
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("入力自身");
        }
    }

    [Test]
    public async Task 出力名が入力名の接頭辞だが別ファイル_入力を保ってbaselineを保存する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var input = directory.Combine("result.json");
        var output = directory.Combine("result");
        _reportStore.Save(RunReportFixture.CreateSample(), input);
        var original = File.ReadAllBytes(input);

        // Act
        var result = Run(_profileLoader.Load(), "baseline-save", "--input", input, "--output", output);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(File.ReadAllBytes(input)).IsEquivalentTo(original);
            await Assert.That(File.ReadAllBytes(output)).IsEquivalentTo(original);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 多段のリンクを経由して入力自身へ出力する_最終リンク先で同一性を判定して拒否する(bool directoryLink)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var actualDirectory = Directory.CreateDirectory(directory.Combine("actual")).FullName;
        var input = Path.Combine(actualDirectory, "run.json");
        _reportStore.Save(RunReportFixture.CreateSample(), input);
        var original = File.ReadAllBytes(input);
        var firstLink = directory.Combine("first");
        var secondLink = directory.Combine("second");
        string alias;
        if (directoryLink)
        {
            Directory.CreateSymbolicLink(secondLink, actualDirectory);
            Directory.CreateSymbolicLink(firstLink, secondLink);
            alias = Path.Combine(firstLink, "run.json");
        }
        else
        {
            File.CreateSymbolicLink(secondLink, input);
            File.CreateSymbolicLink(firstLink, secondLink);
            alias = firstLink;
        }

        // Act
        var result = Run(_profileLoader.Load(), "baseline-save", "--input", alias, "--output", input);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("入力自身");
            await Assert.That(File.ReadAllBytes(input)).IsEquivalentTo(original);
        }
    }

    [Test]
    public async Task Windowsでは大小文字が異なっても同じソース配下_生成前に保護する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var output = Path.Combine(workspace.SourceRoot, "generated");
        var source = OperatingSystem.IsWindows() ? workspace.SourceRoot.ToUpperInvariant() : workspace.SourceRoot;
        var args = GenerateArguments(workspace, output);
        args[2] = source;
        args[4] = source;

        // Act
        var result = Run(workspace.CreateProfile(), args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("--spec-root");
            await Assert.That(Directory.Exists(output)).IsFalse();
        }
    }

    [Test]
    public async Task ソースrootがファイルシステムのroot_その配下の生成先を準備前に拒否する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var output = directory.Combine("generated");

        // Act
        var result = Run(
            _profileLoader.Load(),
            "generate",
            "--spec-root",
            Path.GetPathRoot(directory.Root)!,
            "--wabt-root",
            directory.Combine("wabt"),
            "--wast2json",
            directory.Combine("converter"),
            "--output",
            output
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(2);
            await Assert.That(result.Error).Contains("--spec-root");
            await Assert.That(Directory.Exists(output)).IsFalse();
        }
    }

    private static (int ExitCode, string Output, string Error) Run(ConversionProfile profile, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = new RunnerCli(profile).Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string[] GenerateArguments(SuiteWorkspace workspace, string output)
    {
        return
        [
            "generate",
            "--spec-root",
            workspace.SourceRoot,
            "--wabt-root",
            workspace.SourceRoot,
            "--wast2json",
            SuiteWorkspace.ConverterPath,
            "--output",
            output,
        ];
    }

    private static CorpusManifest Complete(CorpusManifest manifest)
    {
        return manifest with { Summary = manifest.Summarize(), Completion = new(true, true) };
    }

    private static void PrepareScript(CorpusFixture fixture, string scenario)
    {
        var json =
            scenario == "diagnostic"
                ? """{"source_filename":"a.wast","commands":[{"type":"assert_malformed","line":1,"filename":"a.0.wasm","module_type":"binary","text":"一致しない診断"}]}"""
                : """{"source_filename":"a.wast","commands":[{"type":"module","line":1,"filename":"a.0.wasm"}]}""";
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.json", Encoding.UTF8.GetBytes(json));
        if (scenario == "diagnostic")
        {
            fixture.WriteArtifact(CorpusFixture.A, "modules/a.0.wasm", [0]);
        }
        if (scenario == "broken")
        {
            File.WriteAllText(fixture.GetPath("modules/a.json"), "{");
        }
        fixture.Manifest.Inputs[0].Artifacts.RemoveAll(x => x.Path is "modules/a.1.wat" or "modules/a.2.wasm");
        File.Delete(fixture.GetPath("modules/a.1.wat"));
        File.Delete(fixture.GetPath("modules/a.2.wasm"));
    }
}
