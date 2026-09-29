using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusVerifier_VerifySourcesTests
{
    [Test]
    public async Task 全入力の生バイト列が一致する_診断を返さない()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();

        // Act
        var diagnostics = CorpusVerifier.VerifySources(
            fixture.Manifest.Profile.Inputs,
            fixture.InputRoot
        );

        // Assert
        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task 改行だけが異なる入力がある_正規化せず不一致として返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.WriteSource(CorpusFixture.B, "(module)\r\n");

        // Act
        var diagnostics = CorpusVerifier.VerifySources(
            fixture.Manifest.Profile.Inputs,
            fixture.InputRoot
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostics.Length).IsEqualTo(1);
            await Assert.That(diagnostics[0].Path).IsEqualTo(CorpusFixture.B);
            await Assert.That(diagnostics[0].Message).Contains("SHA-256");
        }
    }

    [Test]
    public async Task 欠落と余剰がある_入力pathごとに理由を返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        File.Delete(Path.Combine(fixture.InputRoot, CorpusFixture.A));
        fixture.WriteSource("extra.wast", "(module)\n");
        fixture.WriteSource("simd/note.txt", "対象外の拡張子");

        // Act
        var diagnostics = CorpusVerifier.VerifySources(
            fixture.Manifest.Profile.Inputs,
            fixture.InputRoot
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    diagnostics.Select(x => x.Path).SequenceEqual([CorpusFixture.A, "extra.wast"])
                )
                .IsTrue();
            await Assert.That(diagnostics.All(x => x.Operation == "verify")).IsTrue();
        }
    }
}
