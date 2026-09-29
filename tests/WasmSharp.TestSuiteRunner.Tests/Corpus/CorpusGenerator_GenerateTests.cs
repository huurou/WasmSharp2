using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusGenerator_GenerateTests
{
    [Test]
    public async Task 生成の前提が揃わない_変換を開始せず全入力を未処理のまま診断を保存する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile();
        profile = profile with
        {
            Wabt = profile.Wabt with { Commit = "0000000000000000000000000000000000000000" },
        };

        // Act
        var result = CorpusGenerator.Generate(workspace.CreateRequest(profile));

        // Assert
        var manifest = result.Manifest;
        var stored = ReportStore.ReadManifest(result.ManifestPath);
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNull();
            await Assert
                .That(result.ManifestPath)
                .IsEqualTo(Path.Combine(workspace.OutputRoot, "manifest.json"));
            await Assert.That(manifest.Diagnostics.Count).IsEqualTo(1);
            await Assert
                .That(
                    manifest.Inputs.All(x =>
                        x.Status == ConversionStatus.Unprocessed
                        && x.ExitCode is null
                        && x.Arguments.Count == 0
                        && x.Artifacts.Count == 0
                    )
                )
                .IsTrue();
            await Assert.That(manifest.Summary).IsEqualTo(new ConversionSummary(4, 0, 0, 4, 0));
            await Assert.That(manifest.Completion).IsEqualTo(new ConversionCompletion(false, true));
            await Assert
                .That(
                    Directory
                        .EnumerateFileSystemEntries(workspace.OutputRoot)
                        .Select(Path.GetFileName)
                        .SequenceEqual(["manifest.json"])
                )
                .IsTrue();
            await Assert
                .That(stored.Issues.Any(x => x.Kind == RecordIssueKind.Unprocessed))
                .IsTrue();
            await Assert
                .That(CompletionPolicy.Generate(stored.Manifest, stored.Issues, true).ExitCode)
                .IsEqualTo(2);
        }
    }

    [Test]
    public async Task 成功と失敗と部分生成を含む全入力を生成する_部分生成物を保持し照合がそろった入力だけを成功として保存する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var request = workspace.CreateRequest(workspace.CreateProfile());

        // Act
        var result = CorpusGenerator.Generate(request);

        // Assert
        var manifest = result.Manifest;
        var stored = ReportStore.ReadManifest(result.ManifestPath);
        var success = Get(manifest, "success.wast");
        var failure = Get(manifest, "failure.wast");
        var partial = Get(manifest, "partial.wast");
        var partialFailure = Get(manifest, "partial-failure.wast");
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNull();
            await Assert.That(manifest.Diagnostics).IsEmpty();
            await Assert.That(success.Status).IsEqualTo(ConversionStatus.Succeeded);
            await Assert.That(success.Diagnostics).IsEmpty();
            await Assert
                .That(
                    success
                        .Artifacts.Select(x => (x.Path, x.Kind, x.InputPath))
                        .SequenceEqual([
                            ("modules/success.0.wasm", ArtifactKind.Wasm, "success.wast"),
                            ("modules/success.json", ArtifactKind.Json, "success.wast"),
                        ])
                )
                .IsTrue();
            await Assert.That(success.Artifacts[1].Script!.CommandCount).IsEqualTo(1);
            await Assert
                .That(success.Artifacts[1].Script!.References[0].ArtifactPath)
                .IsEqualTo("modules/success.0.wasm");
            await Assert.That(failure.Status).IsEqualTo(ConversionStatus.RunnerError);
            await Assert.That(failure.Artifacts).IsEmpty();
            await Assert.That(failure.Diagnostics.Any(x => x.Operation == "convert")).IsTrue();
            await Assert.That(partial.ExitCode).IsEqualTo(0);
            await Assert.That(partial.Status).IsEqualTo(ConversionStatus.RunnerError);
            await Assert
                .That(partial.Artifacts.Select(x => x.Path).SequenceEqual(["modules/partial.json"]))
                .IsTrue();
            await Assert
                .That(partial.Diagnostics.Any(x => x.Path == "modules/partial.0.wasm"))
                .IsTrue();
            await Assert.That(partialFailure.Status).IsEqualTo(ConversionStatus.RunnerError);
            await Assert
                .That(
                    partialFailure
                        .Artifacts.Select(x => x.Path)
                        .SequenceEqual(["modules/partial-failure.json"])
                )
                .IsTrue();
            await Assert.That(manifest.Summary).IsEqualTo(new ConversionSummary(4, 1, 3, 0, 4));
            await Assert.That(manifest.Completion).IsEqualTo(new ConversionCompletion(true, true));
            await Assert.That(stored.Issues).IsEmpty();
            await Assert
                .That(JsonSerializer.Serialize(stored.Manifest))
                .IsEqualTo(JsonSerializer.Serialize(manifest));
            await Assert
                .That(CompletionPolicy.Generate(stored.Manifest, stored.Issues, true).ExitCode)
                .IsEqualTo(1);
            await Assert
                .That(await workspace.GitAsync("status", "--porcelain"))
                .IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task 全入力の変換と照合が成功する_全入力を成功として保存し生成の終了条件を満たす()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var specRoot = CopySpec(workspace, "spec-copy", "success.wast");
        var profile = workspace.CreateProfile() with
        {
            Inputs = [.. workspace.CreateProfile().Inputs.Where(x => x.Path == "success.wast")],
        };
        var request = workspace.CreateRequest(profile) with { SpecRoot = specRoot };

        // Act
        var result = CorpusGenerator.Generate(request);

        // Assert
        var stored = ReportStore.ReadManifest(result.ManifestPath);
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNull();
            await Assert.That(result.Manifest.Diagnostics).IsEmpty();
            await Assert
                .That(result.Manifest.Summary)
                .IsEqualTo(new ConversionSummary(1, 1, 0, 0, 2));
            await Assert
                .That(CompletionPolicy.Generate(stored.Manifest, stored.Issues, true).ExitCode)
                .IsEqualTo(0);
        }
    }

    [Test]
    public async Task 入力と出力の配置rootだけを変えて再生成する_生成物の相対一覧とhashを維持する()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var profile = workspace.CreateProfile() with
        {
            Inputs = [.. workspace.CreateProfile().Inputs.Where(x => x.Path == "success.wast")],
        };
        var first = workspace.CreateRequest(profile) with
        {
            SpecRoot = CopySpec(workspace, "first spec", "success.wast"),
            OutputRoot = Path.Combine(workspace.OutputRoot, "first"),
        };
        var second = workspace.CreateRequest(profile) with
        {
            SpecRoot = CopySpec(workspace, Path.Combine("別の 配置", "second"), "success.wast"),
            OutputRoot = Path.Combine(workspace.OutputRoot, "別の出力", "second"),
        };

        // Act
        var firstResult = CorpusGenerator.Generate(first);
        var secondResult = CorpusGenerator.Generate(second);

        // Assert
        var firstArtifacts = firstResult
            .Manifest.Inputs.SelectMany(x => x.Artifacts)
            .Select(x => (x.Path, x.Kind, x.Sha256, x.InputPath))
            .ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(firstArtifacts.Length).IsEqualTo(2);
            await Assert
                .That(
                    secondResult
                        .Manifest.Inputs.SelectMany(x => x.Artifacts)
                        .Select(x => (x.Path, x.Kind, x.Sha256, x.InputPath))
                        .SequenceEqual(firstArtifacts)
                )
                .IsTrue();
            await Assert
                .That(secondResult.Manifest.Provenance.SpecRoot)
                .IsNotEqualTo(firstResult.Manifest.Provenance.SpecRoot);
            await Assert
                .That(secondResult.Manifest.Inputs.All(x => x.Status == ConversionStatus.Succeeded))
                .IsTrue();
        }
    }

    [Test]
    public async Task 保存先に既存のファイルがある_既存ファイルを変更せず保存失敗を返す()
    {
        // Arrange
        using var workspace = await SuiteWorkspace.CreateAsync();
        var manifestPath = Path.Combine(workspace.OutputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, "既存の結果");

        // Act
        var result = CorpusGenerator.Generate(workspace.CreateRequest(workspace.CreateProfile()));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.SaveFailure).IsNotNull();
            await Assert.That(result.SaveFailure!.Path).IsEqualTo(manifestPath);
            await Assert.That(result.Manifest.Completion.OutputComplete).IsFalse();
            await Assert.That(result.Manifest.Completion.ProcessingComplete).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(manifestPath)).IsEqualTo("既存の結果");
        }
    }

    private static InputConversionResult Get(CorpusManifest manifest, string inputPath)
    {
        return manifest.Inputs.Single(x => x.Input.Path == inputPath);
    }

    private static string CopySpec(SuiteWorkspace workspace, string name, params string[] inputs)
    {
        // 一時Gitリポジトリの外側に、Git管理外の同じ入力を置く。
        var specRoot = Path.Combine(Path.GetDirectoryName(workspace.SourceRoot)!, name);
        var inputRoot = Path.Combine(specRoot, "test", "core");
        Directory.CreateDirectory(inputRoot);
        foreach (var input in inputs)
        {
            File.Copy(Path.Combine(workspace.InputRoot, input), Path.Combine(inputRoot, input));
        }

        return specRoot;
    }
}
