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
        fixture.WriteSource("z.wast", "(module)\n");
        fixture.WriteSource("simd/extra.wast", "(module)\n");
        fixture.WriteSource("extra.wast", "(module)\n");
        fixture.WriteSource("simd/note.txt", "対象外の拡張子");
        fixture.WriteSource("simd/note.WAST", "対象外の拡張子");

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
                    diagnostics.Select(x => x.Path).SequenceEqual([
                        CorpusFixture.A,
                        "extra.wast",
                        "simd/extra.wast",
                        "z.wast",
                    ])
                )
                .IsTrue();
            await Assert.That(diagnostics.All(x => x.Operation == "verify")).IsTrue();
            await Assert.That(diagnostics[0].Message).IsEqualTo("元入力がありません。");
            await Assert
                .That(diagnostics.Skip(1).All(x => x.Message == "固定profileにない元入力があります。"))
                .IsTrue();
        }
    }

    [Test]
    public async Task 元入力rootが存在しない_入力欠落だけを返し余剰列挙の例外を追加しない()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var diagnostics = CorpusVerifier.VerifySources(
            [new SourceInput("a.wast", new string('a', 64))],
            directory.Combine("missing")
        );

        // Assert
        await Assert
            .That(diagnostics)
            .IsEquivalentTo(
                [new CorpusDiagnostic("verify", "元入力がありません。", "a.wast")]
            );
    }

    [Test]
    public async Task 入力rootの下位ディレクトリを列挙できない_余剰入力を確認できない理由を返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var restricted = Directory.CreateDirectory(Path.Combine(fixture.InputRoot, "restricted"));
        using var restriction = new DirectoryReadRestriction(restricted.FullName);

        // Act
        var diagnostics = CorpusVerifier.VerifySources(
            fixture.Manifest.Profile.Inputs,
            fixture.InputRoot
        );

        // Assert
        var diagnostic = diagnostics.Single();
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Operation).IsEqualTo("verify");
            await Assert.That(diagnostic.Path).IsNull();
            await Assert.That(diagnostic.Message).StartsWith("元入力の一覧を列挙できません: ");
            await Assert
                .That(diagnostic.ExceptionType)
                .IsEqualTo(typeof(UnauthorizedAccessException).FullName);
        }
    }

    [Test]
    public async Task 元入力が排他的に開かれている_読取例外を入力path付き診断として返す()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        using var stream = File.Open(
            Path.Combine(fixture.InputRoot, CorpusFixture.A),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        );

        // Act
        var diagnostics = CorpusVerifier.VerifySources(
            fixture.Manifest.Profile.Inputs,
            fixture.InputRoot
        );

        // Assert
        var diagnostic = diagnostics.Single();
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Path).IsEqualTo(CorpusFixture.A);
            await Assert.That(diagnostic.ExceptionType).IsEqualTo(typeof(IOException).FullName);
            await Assert.That(diagnostic.Message).StartsWith("元入力を読み取れません: ");
        }
    }
}
