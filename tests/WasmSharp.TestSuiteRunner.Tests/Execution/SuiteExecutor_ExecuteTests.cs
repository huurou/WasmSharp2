using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public class SuiteExecutor_ExecuteTests
{
    [Test]
    public async Task Binaryのhash不一致_対象commandだけを一度runner_errorとして記録する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.2.wasm",
            ScriptExecutionFixture.Exports()
        );
        File.WriteAllBytes(fixture.GetPath("modules/a.0.wasm"), [1]);

        // Act
        var report = SuiteExecutor.Execute(Complete(fixture.Manifest), fixture.ManifestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Inputs[0].Cases[0].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(report.Inputs[0].Cases[0].LastStage).IsNull();
            await Assert
                .That(report.Inputs[0].Cases.Count(x => x.Outcome == CaseOutcome.RunnerError))
                .IsEqualTo(1);
            await Assert.That(report.Inputs[0].Cases[3].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(report.Inputs[0].Issues.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 複数入力のmoduleと登録とspectest_入力間で状態を分離してcommandを一度ずつ記録する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes(
                """
                {"source_filename":"a.wast","commands":[
                 {"type":"module","line":1,"name":"$A","filename":"a.0.wasm"},
                 {"type":"register","line":1,"as":"shared"},
                 {"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[]},
                 {"type":"assert_return","line":1,"action":{"type":"get","field":"g"},"expected":[{"type":"i32","value":"666"}]}]}
                """
            )
        );
        var spectest = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (
                2,
                [
                    2,
                    .. ScriptExecutionFixture.Name("spectest"),
                    .. ScriptExecutionFixture.Name("print"),
                    0,
                    0,
                    .. ScriptExecutionFixture.Name("spectest"),
                    .. ScriptExecutionFixture.Name("global_i32"),
                    3,
                    0x7F,
                    0,
                ]
            ),
            (
                7,
                [
                    2,
                    .. ScriptExecutionFixture.Name("f"),
                    0,
                    0,
                    .. ScriptExecutionFixture.Name("g"),
                    3,
                    0,
                ]
            )
        );
        fixture.WriteArtifact(CorpusFixture.A, "modules/a.0.wasm", spectest);
        fixture.WriteArtifact(
            CorpusFixture.B,
            "modules/simd/b.json",
            Encoding.UTF8.GetBytes(
                """
                {"source_filename":"simd/b.wast","commands":[
                 {"type":"action","line":1,"action":{"type":"get","module":"$A","field":"g"},"expected":[]},
                 {"type":"module","line":1,"filename":"b.0.wasm"},
                 {"type":"assert_return","line":1,"action":{"type":"get","field":"g"},"expected":[{"type":"i32","value":"666"}]},
                 {"type":"module","line":1,"filename":"b.1.wasm"}]}
                """
            )
        );
        fixture.WriteArtifact(CorpusFixture.B, "modules/simd/b.0.wasm", spectest);
        fixture.WriteArtifact(
            CorpusFixture.B,
            "modules/simd/b.1.wasm",
            ScriptExecutionFixture.ImportFunctions("shared")
        );
        var manifest = Complete(fixture.Manifest);

        // Act
        var report = SuiteExecutor.Execute(manifest, fixture.ManifestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(report.Inputs.All(x => x.Status == InputRunStatus.Processed))
                .IsTrue();
            await Assert.That(report.Summary.EnumeratedCommandCount).IsEqualTo(8);
            await Assert
                .That(report.Inputs.SelectMany(x => x.Cases).Select(x => x.Id).Distinct().Count())
                .IsEqualTo(8);
            await Assert
                .That(report.Inputs[0].Cases.All(x => x.Outcome == CaseOutcome.Passed))
                .IsTrue();
            await Assert.That(report.Inputs[0].Cases[2].Prints[0].Function).IsEqualTo("print");
            await Assert.That(report.Inputs[1].Cases[0].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(report.Inputs[1].Cases[2].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(report.Inputs[1].Cases[3].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert
                .That(report.Inputs[1].Cases[3].Diagnostics[0].Reason)
                .IsEqualTo("MissingImport");
            await Assert.That(report.ExecutionPolicy.MaxCallDepth).IsEqualTo(1024);
            await Assert.That(report.Provenance.RuntimeVersion).IsNotNull();
        }
    }

    [Test]
    public async Task 破損JSONとtext素材の欠落_件数未確定と入力異常とcommand分類を分ける()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        File.WriteAllText(fixture.GetPath("modules/a.json"), "changed");
        fixture.WriteArtifact(
            CorpusFixture.B,
            "modules/simd/b.json",
            Encoding.UTF8.GetBytes(
                """
                {"source_filename":"simd/b.wast","commands":[
                 {"type":"assert_malformed","line":1,"filename":"b.0.wat","module_type":"text","text":"expected"},
                 {"type":"unknown","line":1}]}
                """
            )
        );
        fixture.WriteArtifact(
            CorpusFixture.B,
            "modules/simd/b.0.wat",
            Encoding.UTF8.GetBytes("(module")
        );
        File.Delete(fixture.GetPath("modules/simd/b.0.wat"));

        // Act
        var report = SuiteExecutor.Execute(Complete(fixture.Manifest), fixture.ManifestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Inputs[0].CommandCount).IsNull();
            await Assert.That(report.Inputs[0].Cases.Count).IsEqualTo(0);
            await Assert.That(report.Inputs[0].Issues.Count > 0).IsTrue();
            await Assert.That(report.Inputs[1].Cases[0].Outcome).IsEqualTo(CaseOutcome.OutOfScope);
            await Assert.That(report.Inputs[1].Issues.Count > 0).IsTrue();
            await Assert.That(report.Summary.UndeterminedInputCount).IsEqualTo(1);
            await Assert.That(report.Summary.UncategorizedRunnerErrorCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 変換に失敗した入力_照合異常をIssuesへ保持して独立入力を続行する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.UpdateInput(
            CorpusFixture.A,
            x =>
                x with
                {
                    Status = ConversionStatus.RunnerError,
                    Diagnostics = [new("convert", "conversion failed", CorpusFixture.A)],
                }
        );

        // Act
        var report = SuiteExecutor.Execute(Complete(fixture.Manifest), fixture.ManifestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Inputs[0].Issues.Count > 0).IsTrue();
            await Assert.That(report.Inputs[1].Cases[0].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(report.Completion.ProcessingComplete).IsTrue();
        }
    }

    [Test]
    public async Task 開始前に中断_入力とcommandを未処理のまま保持する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var report = SuiteExecutor.Execute(
            Complete(fixture.Manifest),
            fixture.ManifestPath,
            cancellation.Token
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Completion.ProcessingComplete).IsFalse();
            await Assert.That(report.Inputs.SelectMany(x => x.Cases).Count()).IsEqualTo(0);
            await Assert.That(report.Inputs[1].Status).IsEqualTo(InputRunStatus.Unprocessed);
            await Assert.That(report.Diagnostics.Count).IsEqualTo(1);
        }
    }

    internal static CorpusManifest Complete(CorpusManifest manifest)
    {
        return manifest with { Summary = manifest.Summarize(), Completion = new(true, true) };
    }
}
