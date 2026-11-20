using WasmSharp.Tests.Fixtures;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public partial class SuiteExecutor_ExecuteAndSaveTests
{
    private readonly ReportStore _reportStore = new ReportStore();

    [Test]
    public async Task Binary素材が欠落する_対象素材のpathと元診断を保存して読み戻せる()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        File.Delete(fixture.GetPath("modules/a.0.wasm"));
        var output = Path.Combine(fixture.OutputRoot, "missing-material.json");

        // Act
        var result = SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core2,
            output,
            _reportStore
        );
        var stored = _reportStore.ReadRunReport(output);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNull();
            await Assert.That(stored.Issues).IsEmpty();
            var actual = stored.Report.Inputs[0].Cases[0];
            await Assert.That(actual.Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(actual.LastStage).IsNull();
            await Assert.That(actual.Diagnostics[0].Path).IsEqualTo("modules/a.0.wasm");
            await Assert
                .That(actual.Diagnostics[0].Message)
                .IsEqualTo(result.Report.Inputs[0].Cases[0].Diagnostics[0].Message);
        }
    }

    [Test]
    public async Task 途中まで列挙できるJSON_確定commandだけを保存し未確定件数は終了1になる()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            System.Text.Encoding.UTF8.GetBytes(
                """
                {"source_filename":"a.wast","commands":[{"type":"module","line":1,"filename":"a.0.wasm"},
                """
            )
        );
        var output = Path.Combine(fixture.OutputRoot, "partial.json");

        // Act
        var result = SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core2,
            output,
            _reportStore
        );

        // Assert
        var stored = _reportStore.ReadRunReport(output);
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNull();
            await Assert.That(stored.Report.Inputs[0].CommandCount).IsNull();
            await Assert.That(stored.Report.Inputs[0].Cases.Count).IsEqualTo(1);
            await Assert.That(stored.Report.Inputs[0].Cases[0].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(stored.Report.Inputs[1].Cases[0].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(CompletionPolicy.Run(stored.Report, stored.Issues, true).ExitCode).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 中断した実行_未処理を残した結果を保存して終了2になる()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var output = Path.Combine(fixture.OutputRoot, "interrupted.json");

        // Act
        var result = SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core2,
            output,
            _reportStore,
            cancellationToken: cancellation.Token
        );

        // Assert
        var stored = _reportStore.ReadRunReport(output);
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNull();
            await Assert.That(stored.Report.Completion.OutputComplete).IsTrue();
            await Assert.That(stored.Report.Completion.ProcessingComplete).IsFalse();
            await Assert.That(stored.Report.Inputs.All(x => x.Status == InputRunStatus.Unprocessed)).IsTrue();
            await Assert.That(CompletionPolicy.Run(stored.Report, stored.Issues, true).ExitCode).IsEqualTo(2);
        }
    }

    [Test]
    public async Task 素材を移動し元WASTを参照できない_全結果を保存してrunとverifyが成功する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.2.wasm", ScriptExecutionFixture.Exports());
        Directory.Move(fixture.SourceRoot, Path.Combine(fixture.Root, "unused-source"));
        fixture.MoveOutput();
        var manifest = SuiteExecutor_ExecuteTests.Complete(fixture.Manifest);
        var output = Path.Combine(fixture.OutputRoot, "run.json");

        // Act
        var result = SuiteExecutor.ExecuteAndSave(
            manifest,
            fixture.ManifestPath,
            WasmSpecVersion.Core2,
            output,
            _reportStore
        );

        // Assert
        var stored = _reportStore.ReadRunReport(output);
        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(output)).IsTrue();
            await Assert.That(result.SaveFailure).IsNull();
            await Assert.That(stored.Issues.IsEmpty).IsTrue();
            await Assert.That(stored.Report.Summary.EnumeratedCommandCount).IsEqualTo(5);
            await Assert.That(CompletionPolicy.Run(stored.Report, stored.Issues, true).ExitCode).IsEqualTo(0);
            await Assert
                .That(CompletionPolicy.Verify(stored.Report, stored.Issues, manifest.Profile.ToProfile()).ExitCode)
                .IsEqualTo(0);
            await Assert.That(ReferenceEquals(result.Report.Corpus.Inputs, manifest.Inputs)).IsFalse();
        }
    }

    [Test]
    public async Task 出力先が既存ファイル_既存内容を保護し保存失敗と終了2を返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var output = Path.Combine(fixture.OutputRoot, "run.json");
        File.WriteAllText(output, "existing result");

        // Act
        var result = SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core2,
            output,
            _reportStore
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(File.ReadAllText(output)).IsEqualTo("existing result");
            await Assert.That(result.SaveFailure).IsNotNull();
            await Assert.That(result.SaveFailure!.Path).IsEqualTo(output);
            await Assert.That(result.Report.Completion.OutputComplete).IsFalse();
            await Assert
                .That(CompletionPolicy.Run(result.Report, ReportStore.Validate(result.Report), false).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task 公式のタグと複数メモリをCore3で実行_タグの未実装と複数メモリの成立を保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create(new ConversionProfileLoader().LoadCore3());
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.0.wasm", Core3OfficialBinary.Tag);
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.2.wasm", Core3OfficialBinary.MultipleMemories);
        var output = Path.Combine(fixture.OutputRoot, "core3.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.RuntimeUnsupported);
            await Assert.That(cases[0].LastStage).IsEqualTo(CaseStage.Decode);
            await Assert.That(cases[0].Diagnostics[0].Feature).IsEqualTo("tag section");
            await Assert.That(cases[0].Diagnostics[0].Location!.Stage).IsEqualTo("Decode");
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(cases[2].LastStage).IsEqualTo(CaseStage.Instantiate);
        }
    }

    [Test]
    public async Task 公式のタグと複数メモリをCore2で実行_未実装へ読み替えず従来の段階の失敗として保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create(new ConversionProfileLoader().Load());
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.0.wasm", Core3OfficialBinary.Tag);
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.2.wasm", Core3OfficialBinary.MultipleMemories);
        var output = Path.Combine(fixture.OutputRoot, "core2.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core2,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[0].LastStage).IsEqualTo(CaseStage.Decode);
            await Assert.That(cases[0].Diagnostics[0].Message).StartsWith("malformed section id");
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[2].LastStage).IsEqualTo(CaseStage.Validate);
            await Assert.That(cases[2].Diagnostics[0].Message).StartsWith("multiple memories");
        }
    }
}
