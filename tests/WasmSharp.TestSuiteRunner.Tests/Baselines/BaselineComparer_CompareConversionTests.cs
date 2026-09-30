using System.Text.Json;
using WasmSharp.TestSuiteRunner.Baselines;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Baselines;

internal class BaselineComparer_CompareConversionTests
{
    [Test]
    public async Task 内容が同じで配置rootや日時や起動引数だけが異なる_再現性が一致する()
    {
        // Arrange
        var baseline = CreateManifest();
        var current = baseline.CreateSnapshot() with
        {
            Provenance = baseline.Provenance with
            {
                CreatedAt = DateTimeOffset.UtcNow,
                SpecRoot = "/moved/spec",
                WabtRoot = "/moved/wabt",
                OutputRoot = "/moved/output",
                ExecutablePath = "/moved/wast2json",
                SpecOrigin = "https://mirror.invalid/spec",
            },
        };
        current.Inputs[0].Arguments.Add("/moved/output/a.json");
        current.Inputs.Reverse();
        current.Profile.Inputs.Reverse();
        current.Profile.Features.Reverse();

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Comparison).IsEqualTo(ComparisonType.Conversion);
            await Assert.That(comparison.Established).IsTrue();
            await Assert.That(comparison.Complete).IsTrue();
            await Assert.That(comparison.ConditionDifferences).IsEmpty();
            await Assert.That(comparison.EntryDifferences).IsEmpty();
            await Assert.That(comparison.Uncompared).IsEmpty();
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(0);
        }
    }

    [Test]
    public async Task 実行ファイルhashだけが異なる_出典差を残して再現性比較を継続する()
    {
        // Arrange
        var baseline = CreateManifest();
        var current = baseline.CreateSnapshot() with
        {
            Provenance = baseline.Provenance with { ExecutableSha256 = new string('e', 64) },
        };

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(comparison.ProvenanceDifferences.Single().Baseline)
                .IsEqualTo(baseline.Provenance.ExecutableSha256);
            await Assert
                .That(comparison.ProvenanceDifferences.Single().Current)
                .IsEqualTo(current.Provenance.ExecutableSha256);
            await Assert.That(comparison.Summary.ProvenanceDifferenceCount).IsEqualTo(1);
            await Assert.That(comparison.ConditionDifferences).IsEmpty();
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("profile.id")]
    [Arguments("spec.url")]
    [Arguments("spec.commit")]
    [Arguments("wabt.url")]
    [Arguments("wabt.commit")]
    [Arguments("working_directory")]
    [Arguments("conversion.check")]
    [Arguments("conversion.canonical_lebs")]
    [Arguments("conversion.relocatable")]
    [Arguments("conversion.debug_names")]
    [Arguments("logical_arguments")]
    [Arguments("feature.default_enabled")]
    [Arguments("feature.enabled")]
    [Arguments("feature.added")]
    [Arguments("feature.missing")]
    public async Task 変換条件が異なる_変更前後の条件を残して非0を返す(string condition)
    {
        // Arrange
        var baseline = CreateManifest();
        var current = ChangeCondition(baseline.CreateSnapshot(), condition);

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Established).IsTrue();
            await Assert.That(comparison.Complete).IsTrue();
            await Assert.That(comparison.ConditionDifferences).IsNotEmpty();
            await Assert
                .That(comparison.ConditionDifferences.All(x => x.Baseline != x.Current))
                .IsTrue();
            await Assert
                .That(comparison.Summary.ConditionDifferenceCount)
                .IsEqualTo(comparison.ConditionDifferences.Count);
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("profile_input.sha256")]
    [Arguments("input.sha256")]
    [Arguments("artifact.sha256")]
    [Arguments("artifact.kind")]
    [Arguments("artifact.input_path")]
    [Arguments("conversion.status")]
    public async Task 入力や生成物や変換状態が異なる_対象と属性の前後差を残す(string property)
    {
        // Arrange
        var baseline = CreateManifest();
        var current = baseline.CreateSnapshot();
        var input = current.Inputs[0];
        switch (property)
        {
            case "profile_input.sha256":
                current.Profile.Inputs[0] = current.Profile.Inputs[0] with
                {
                    Sha256 = new string('f', 64),
                };
                break;
            case "input.sha256":
                current.Inputs[0] = input with
                {
                    Input = input.Input with { Sha256 = new string('f', 64) },
                };
                break;
            case "artifact.sha256":
                input.Artifacts[0] = input.Artifacts[0] with { Sha256 = new string('f', 64) };
                break;
            case "artifact.kind":
                input.Artifacts[0] = input.Artifacts[0] with { Kind = ArtifactKind.Wat };
                break;
            case "artifact.input_path":
                input.Artifacts[0] = input.Artifacts[0] with { InputPath = "b.wast" };
                break;
            case "conversion.status":
                current.Inputs[0] = input with { Status = ConversionStatus.RunnerError };
                break;
        }
        current = current with { Summary = current.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(comparison.EntryDifferences.Any(x => x.Property == property))
                .IsTrue();
            await Assert
                .That(comparison.Summary.EntryDifferenceCount)
                .IsEqualTo(comparison.EntryDifferences.Count);
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task 入力や生成物が欠落し別の生成物が追加される_有無と未完了の理由を残す()
    {
        // Arrange
        var baseline = CreateManifest();
        var current = baseline.CreateSnapshot();
        current.Inputs.RemoveAt(1);
        current.Inputs[0].Artifacts.Clear();
        current
            .Inputs[0]
            .Artifacts.Add(
                new("modules/new.wasm", ArtifactKind.Wasm, new string('f', 64), "a.wast")
            );
        current = current with { Summary = current.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, current);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(comparison.EntryDifferences.Any(x => x.Path == "b.wast" && x.Current is null))
                .IsTrue();
            await Assert
                .That(
                    comparison.EntryDifferences.Any(x =>
                        x.Path == "modules/a.json" && x.Current is null
                    )
                )
                .IsTrue();
            await Assert
                .That(
                    comparison.EntryDifferences.Any(x =>
                        x.Path == "modules/new.wasm" && x.Baseline is null
                    )
                )
                .IsTrue();
            await Assert.That(comparison.Complete).IsFalse();
            await Assert.That(comparison.Uncompared).IsNotEmpty();
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    [Arguments("未処理")]
    [Arguments("出力失敗")]
    [Arguments("集計不整合")]
    [Arguments("生成物重複")]
    [Arguments("入力重複")]
    [Arguments("feature重複")]
    public async Task 同じ内容でも未完了または対応不能な記録がある_再現性一致としない(string issue)
    {
        // Arrange
        var baseline = CreateManifest();
        switch (issue)
        {
            case "未処理":
                baseline.Inputs[0] = baseline.Inputs[0] with
                {
                    Status = ConversionStatus.Unprocessed,
                };
                break;
            case "出力失敗":
                baseline = baseline with { Completion = new(true, false) };
                break;
            case "集計不整合":
                baseline = baseline with { Summary = new(0, 0, 0, 0, 0) };
                break;
            case "生成物重複":
                baseline.Inputs[0].Artifacts.Add(baseline.Inputs[0].Artifacts[0]);
                break;
            case "入力重複":
                baseline.Inputs.Add(baseline.Inputs[0]);
                break;
            case "feature重複":
                baseline.Profile.Features.Add(baseline.Profile.Features[0]);
                break;
        }
        if (issue != "集計不整合")
        {
            baseline = baseline with { Summary = baseline.Summarize() };
        }

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, baseline.CreateSnapshot());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Complete).IsFalse();
            await Assert.That(comparison.Uncompared).IsNotEmpty();
            await Assert
                .That(comparison.Summary.UncomparedCount)
                .IsEqualTo(comparison.Uncompared.Count);
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task 差分がなくても現結果にrunner_errorがある_再現性比較を成功にしない()
    {
        // Arrange
        var baseline = CreateManifest();
        baseline.Inputs[0] = baseline.Inputs[0] with { Status = ConversionStatus.RunnerError };
        baseline = baseline with { Summary = baseline.Summarize() };

        // Act
        var comparison = BaselineComparer.CompareConversion(baseline, baseline.CreateSnapshot());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(comparison.Complete).IsTrue();
            await Assert.That(comparison.Summary.CurrentRunnerErrorCount).IsEqualTo(1);
            await Assert
                .That(CompletionPolicy.CompareConversion(comparison, saved: true).ExitCode)
                .IsEqualTo(1);
        }
    }

    [Test]
    public async Task 保存済み結果を比較し別ファイルへ出力する_比較元の内容を変更しない()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var baselinePath = directory.Combine("baseline.json");
        var currentPath = directory.Combine("current.json");
        var outputPath = directory.Combine("comparison.json");
        ReportStore.Save(CreateManifest(), baselinePath);
        ReportStore.Save(CreateManifest(), currentPath);
        var original = await File.ReadAllTextAsync(baselinePath);

        // Act
        var comparison = BaselineComparer.CompareConversion(
            ReportStore.ReadManifest(baselinePath).Manifest,
            ReportStore.ReadManifest(currentPath).Manifest
        );
        ReportStore.Save(comparison, outputPath);

        // Assert
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        using (Assert.Multiple())
        {
            await Assert.That(await File.ReadAllTextAsync(baselinePath)).IsEqualTo(original);
            await Assert.That(document.RootElement.GetProperty("complete").GetBoolean()).IsTrue();
            await Assert
                .That(document.RootElement.GetProperty("kind").GetString())
                .IsEqualTo("comparison_report");
        }
    }

    private static CorpusManifest CreateManifest()
    {
        return RunReportFixture.CreateManifest(("a.wast", 1), ("b.wast", 1));
    }

    private static CorpusManifest ChangeCondition(CorpusManifest manifest, string condition)
    {
        var profile = manifest.Profile;
        profile = condition switch
        {
            "profile.id" => profile with { Id = "changed" },
            "spec.url" => profile with
            {
                Spec = profile.Spec with { Url = "https://changed.invalid/spec" },
            },
            "spec.commit" => profile with
            {
                Spec = profile.Spec with { Commit = new string('a', 40) },
            },
            "wabt.url" => profile with
            {
                Wabt = profile.Wabt with { Url = "https://changed.invalid/wabt" },
            },
            "wabt.commit" => profile with
            {
                Wabt = profile.Wabt with { Commit = new string('b', 40) },
            },
            "working_directory" => profile with { WorkingDirectory = "changed" },
            "conversion.check" => profile with
            {
                Conversion = profile.Conversion with { Check = !profile.Conversion.Check },
            },
            "conversion.canonical_lebs" => profile with
            {
                Conversion = profile.Conversion with
                {
                    CanonicalLebs = !profile.Conversion.CanonicalLebs,
                },
            },
            "conversion.relocatable" => profile with
            {
                Conversion = profile.Conversion with
                {
                    Relocatable = !profile.Conversion.Relocatable,
                },
            },
            "conversion.debug_names" => profile with
            {
                Conversion = profile.Conversion with
                {
                    DebugNames = !profile.Conversion.DebugNames,
                },
            },
            _ => profile,
        };
        switch (condition)
        {
            case "logical_arguments":
                profile.LogicalArguments.Add("--changed");
                break;
            case "feature.default_enabled":
                profile.Features[0] = profile.Features[0] with
                {
                    DefaultEnabled = !profile.Features[0].DefaultEnabled,
                };
                break;
            case "feature.enabled":
                profile.Features[0] = profile.Features[0] with
                {
                    Enabled = !profile.Features[0].Enabled,
                };
                break;
            case "feature.added":
                profile.Features.Add(new("changed", false, false));
                break;
            case "feature.missing":
                profile.Features.RemoveAt(0);
                break;
        }
        return manifest with { Profile = profile };
    }
}
