using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusManifest_CreateSnapshotTests
{
    [Test]
    public async Task 部分生成のsnapshotをJSONで往復する_対象集合と状態と素材の対応と出典を保持する()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        // Act
        var snapshot = manifest.CreateSnapshot();
        var json = JsonSerializer.Serialize(snapshot, options);
        var restored = JsonSerializer.Deserialize<CorpusManifest>(json, options)!;
        using var document = JsonDocument.Parse(json);
        var serializedInputs = document.RootElement.GetProperty("inputs");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(restored.Profile.Inputs.Count).IsEqualTo(3);
            await Assert
                .That(
                    restored
                        .Inputs.Select(x => x.Status)
                        .SequenceEqual([
                            ConversionStatus.RunnerError,
                            ConversionStatus.Succeeded,
                            ConversionStatus.Unprocessed,
                        ])
                )
                .IsTrue();
            await Assert.That(restored.Summary).IsEqualTo(new ConversionSummary(3, 1, 1, 1, 4));
            await Assert.That(restored.Completion).IsEqualTo(new ConversionCompletion(false, true));
            await Assert.That(restored.Inputs[0].ExitCode).IsEqualTo(7);
            await Assert.That(restored.Inputs[0].StandardError).IsEqualTo("conversion failed\n");
            await Assert.That(restored.Inputs[0].Artifacts[1].Kind).IsEqualTo(ArtifactKind.Wat);
            await Assert.That(restored.Inputs[0].Artifacts[1].InputPath).IsEqualTo("failed.wast");
            await Assert.That(restored.Inputs[0].Artifacts[0].Script!.CommandCount).IsNull();
            await Assert
                .That(restored.Inputs[0].Artifacts[0].Script!.Commands[1])
                .IsEqualTo(new ArtifactCommand(1, null, null, null, null));
            await Assert
                .That(restored.Inputs[0].Artifacts[0].Script!.References[0])
                .IsEqualTo(new ArtifactReference(0, "failed.0.wat", "modules/failed.0.wat"));
            await Assert
                .That(restored.Inputs[0].Diagnostics[1].ExceptionType)
                .IsEqualTo("System.Text.Json.JsonException");
            await Assert.That(restored.Inputs[1].Artifacts[0].Script!.EnumerationComplete).IsTrue();
            await Assert.That(restored.Inputs[1].Artifacts[1].Kind).IsEqualTo(ArtifactKind.Wasm);
            await Assert.That(restored.Inputs[2].Artifacts.Count).IsEqualTo(0);
            await Assert.That(restored.Provenance).IsEqualTo(manifest.Provenance);
            await Assert
                .That(restored.Profile.Spec.Url)
                .IsEqualTo("https://github.com/WebAssembly/spec.git");
            await Assert
                .That(restored.Provenance.SpecOrigin)
                .IsEqualTo("https://mirror.invalid/spec");
            await Assert
                .That(serializedInputs[0].GetProperty("status").GetString())
                .IsEqualTo("runner_error");
            await Assert
                .That(serializedInputs[1].GetProperty("status").GetString())
                .IsEqualTo("succeeded");
            await Assert
                .That(serializedInputs[2].GetProperty("status").GetString())
                .IsEqualTo("unprocessed");
            await Assert
                .That(
                    serializedInputs[0].GetProperty("artifacts")[0].GetProperty("kind").GetString()
                )
                .IsEqualTo("json");
            await Assert
                .That(
                    serializedInputs[0].GetProperty("artifacts")[1].GetProperty("kind").GetString()
                )
                .IsEqualTo("wat");
            await Assert
                .That(
                    serializedInputs[1].GetProperty("artifacts")[1].GetProperty("kind").GetString()
                )
                .IsEqualTo("wasm");
            await Assert.That(JsonSerializer.Serialize(restored, options)).IsEqualTo(json);
        }
    }

    [Test]
    public async Task 元manifestの入れ子の一覧を変更する_実行結果へ渡すsnapshotは変化しない()
    {
        // Arrange
        var manifest = CorpusManifestFixture.Create();
        var snapshot = manifest.CreateSnapshot();
        var before = JsonSerializer.Serialize(snapshot);

        // Act
        manifest.Profile.Inputs.Clear();
        manifest.Profile.Features.Clear();
        manifest.Profile.LogicalArguments.Clear();
        manifest.Diagnostics.Clear();
        foreach (var input in manifest.Inputs)
        {
            input.Arguments.Clear();
            input.Diagnostics.Clear();
            foreach (var artifact in input.Artifacts)
            {
                artifact.Script?.Commands.Clear();
                artifact.Script?.References.Clear();
            }
            input.Artifacts.Clear();
        }
        manifest.Inputs.Clear();

        // Assert
        await Assert.That(JsonSerializer.Serialize(snapshot)).IsEqualTo(before);
    }
}
