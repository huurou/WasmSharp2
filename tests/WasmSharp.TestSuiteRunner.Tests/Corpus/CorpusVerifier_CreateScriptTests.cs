using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class CorpusVerifier_CreateScriptTests
{
    [Test]
    public async Task 入れ子の入力のJSONから作る_参照をJSONの親基準のmanifest相対pathへ解決する()
    {
        // Arrange
        var document = ScriptDocument.Parse(
            Encoding.UTF8.GetBytes(CorpusFixture.B_JSON),
            "modules/simd/b.json"
        );

        // Act
        var script = CorpusVerifier.CreateScript(document, "modules/simd/b.json");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(script.SourceFilename).IsEqualTo(CorpusFixture.B);
            await Assert.That(script.EnumerationComplete).IsTrue();
            await Assert.That(script.CommandCount).IsEqualTo(1);
            await Assert
                .That(script.Commands.SequenceEqual([new(0, 1, "module", null, "b.0.wasm")]))
                .IsTrue();
            await Assert
                .That(
                    script.References.SequenceEqual([new(0, "b.0.wasm", "modules/simd/b.0.wasm")])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 素材領域外を指す参照名を含む_参照一覧から除きcommand一覧は保持する()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast", "commands": [
              {"type": "module", "line": 1, "filename": "../../a.0.wasm"},
              {"type": "module", "line": 2, "filename": "a.1.wasm"}]}
            """;
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), "modules/a.json");

        // Act
        var script = CorpusVerifier.CreateScript(document, "modules/a.json");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(script.Commands.Count).IsEqualTo(2);
            await Assert.That(script.Commands[0].Filename).IsEqualTo("../../a.0.wasm");
            await Assert
                .That(script.References.SequenceEqual([new(1, "a.1.wasm", "modules/a.1.wasm")]))
                .IsTrue();
        }
    }

    [Test]
    public async Task 構文破損したJSONから作る_確定済みcommandと未確定の総数を保持する()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast", "commands": [{"type": "module", "line": 1, "filename": "a.0.wasm"}, {
            """;
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), "modules/a.json");

        // Act
        var script = CorpusVerifier.CreateScript(document, "modules/a.json");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(script.EnumerationComplete).IsFalse();
            await Assert.That(script.CommandCount).IsNull();
            await Assert.That(script.Commands.Count).IsEqualTo(1);
            await Assert.That(script.References.Count).IsEqualTo(1);
        }
    }
}
