using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusManifest_CreateTests
{
    [Test]
    public async Task 生成開始時のmanifestを作る_生成物がなくても全対象を未処理で保持する()
    {
        // Arrange
        var profile = Core2Profile.Load();
        var provenance = new ConversionProvenance { ExecutableSha256 = new string('a', 64) };

        // Act
        var manifest = CorpusManifest.Create(profile, provenance);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(manifest.Profile.Inputs.SequenceEqual(profile.Inputs)).IsTrue();
            await Assert
                .That(manifest.Inputs.Select(x => x.Input).SequenceEqual(profile.Inputs))
                .IsTrue();
            await Assert
                .That(
                    manifest.Inputs.All(x =>
                        x.Status == ConversionStatus.Unprocessed
                        && x.Artifacts.Count == 0
                        && x.Diagnostics.Count == 0
                        && x.ExitCode is null
                    )
                )
                .IsTrue();
            await Assert.That(manifest.Summary).IsEqualTo(new ConversionSummary(147, 0, 0, 147, 0));
            await Assert
                .That(manifest.Completion)
                .IsEqualTo(new ConversionCompletion(false, false));
            await Assert
                .That(manifest.Provenance.ExecutableSha256)
                .IsEqualTo(provenance.ExecutableSha256);
            await Assert.That(manifest.Diagnostics.Count).IsEqualTo(0);
        }
    }
}
