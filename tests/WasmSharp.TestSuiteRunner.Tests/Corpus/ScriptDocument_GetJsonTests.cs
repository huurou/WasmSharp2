using System.Text;
using System.Text.Json;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ScriptDocument_GetJsonTests
{
    [Test]
    public async Task 列挙済みcommandを取得する_空白を含む元のバイト列を加工せず返す()
    {
        // Arrange
        const string FIRST = """{"type": "module",  "line": 1, "filename": "a.0.wasm"}""";
        const string SECOND = """
            {"type": "assert_return", "line": 2,
                "action": {"type": "invoke", "field": "f", "args": [{"type": "f32", "value": "2143289344"}]},
                "expected": [{"type": "f32", "value": "nan:canonical"}]}
            """;
        var json = $$"""{"source_filename": "a.wast", "commands": [{{FIRST}}, {{SECOND}}]}""";
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(json), "modules/a.json");

        // Act
        var first = document.GetJson(0);
        var second = document.GetJson(1);
        using var parsed = JsonDocument.Parse(second);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Encoding.UTF8.GetString(first.Span)).IsEqualTo(FIRST);
            await Assert.That(Encoding.UTF8.GetString(second.Span)).IsEqualTo(SECOND);
            await Assert
                .That(document.Commands[1].Start)
                .IsEqualTo(json.IndexOf(SECOND, StringComparison.Ordinal));
            await Assert
                .That(
                    parsed.RootElement.GetProperty("expected")[0].GetProperty("value").GetString()
                )
                .IsEqualTo("nan:canonical");
        }
    }

    [Test]
    public async Task 構文破損前の確定済みcommandを取得する_破損箇所を含まない範囲を返す()
    {
        // Arrange
        const string FIRST = """{"type": "module", "line": 1}""";
        var json = $$"""{"source_filename": "a.wast", "commands": [{{FIRST}}, {"type": ]}""";
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(json), "modules/a.json");

        // Act
        var first = document.GetJson(0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.Commands.Length).IsEqualTo(1);
            await Assert.That(Encoding.UTF8.GetString(first.Span)).IsEqualTo(FIRST);
        }
    }
}
