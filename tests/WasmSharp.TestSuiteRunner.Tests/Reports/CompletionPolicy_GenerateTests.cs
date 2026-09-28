using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CompletionPolicy_GenerateTests
{
    [Test]
    public async Task 全入力の変換と照合と保存が完了した_成功の0を返す()
    {
        // Arrange
        var manifest = RunReportFixture.CreateManifest(("a.wast", 1), ("b.wast", 2));

        // Act
        var decision = CompletionPolicy.Generate(
            manifest,
            ReportStore.Validate(manifest),
            saved: true
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(0);
            await Assert.That(decision.Reasons).IsEmpty();
        }
    }

    [Test]
    public async Task 変換に失敗した入力を記録して保存した_用途不合格の1を返す()
    {
        // Arrange
        var manifest = RunReportFixture.CreateManifest(("a.wast", 1), ("b.wast", 2));
        manifest.Inputs[1] = manifest.Inputs[1] with { Status = ConversionStatus.RunnerError };
        manifest = manifest with { Summary = manifest.Summarize() };

        // Act
        var decision = CompletionPolicy.Generate(
            manifest,
            ReportStore.Validate(manifest),
            saved: true
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.Status).IsEqualTo(CompletionStatus.Unsatisfied);
            await Assert.That(decision.ExitCode).IsEqualTo(1);
            await Assert.That(decision.Reasons.Single()).Contains("1件");
        }
    }

    [Test]
    public async Task 変換失敗と未処理の入力が残る_未完了の2を優先し両方の理由を返す()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create();

        // Act
        var decision = CompletionPolicy.Generate(
            manifest,
            ReportStore.Validate(manifest),
            saved: true
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(2);
            await Assert.That(decision.Reasons.Any(x => x.Contains("unprocessed.wast"))).IsTrue();
            await Assert
                .That(decision.Reasons.Any(x => x.Contains("変換・照合に失敗した入力")))
                .IsTrue();
        }
    }

    [Test]
    public async Task Manifestを保存できなかった_未完了の2を返す()
    {
        // Arrange
        var manifest = RunReportFixture.CreateManifest(("a.wast", 1));

        // Act
        var decision = CompletionPolicy.Generate(
            manifest,
            ReportStore.Validate(manifest),
            saved: false
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decision.ExitCode).IsEqualTo(2);
            await Assert.That(decision.Reasons.Single()).Contains("保存");
        }
    }
}
