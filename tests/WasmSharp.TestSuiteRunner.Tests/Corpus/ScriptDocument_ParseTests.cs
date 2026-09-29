using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ScriptDocument_ParseTests
{
    private const string PATH = "modules/a.json";

    [Test]
    public async Task 固定形式のJSONを読む_全commandの順序と取得可能な項目を保持して列挙を完了する()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast",
             "commands": [
              {"type": "module", "line": 1, "name": "$M", "filename": "a.0.wasm"},
              {"type": "register", "line": 2, "name": "$M", "as": "M"},
              {"type": "assert_malformed", "line": 3, "filename": "a.1.wat", "text": "unexpected token", "module_type": "text"},
              {"type": "assert_return", "line": 3, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "i32", "value": "1"}]}]}
            """;

        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.SourceFilename).IsEqualTo("a.wast");
            await Assert.That(document.EnumerationComplete).IsTrue();
            await Assert.That(document.CommandCount).IsEqualTo(4);
            await Assert.That(document.Diagnostics).IsEmpty();
            await Assert
                .That(
                    document
                        .Commands.Select(x => (x.Index, x.Line, x.Type, x.ModuleType, x.Filename))
                        .SequenceEqual([
                            (0, 1, "module", null, "a.0.wasm"),
                            (1, 2, "register", null, null),
                            (2, 3, "assert_malformed", "text", "a.1.wat"),
                            (3, 3, "assert_return", null, null),
                        ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 途中で構文が破損したJSONを読む_確定済みprefixだけを残して総数を未確定にする()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast",
             "commands": [
              {"type": "module", "line": 1, "filename": "a.0.wasm"},
              {"type": "action", "line": 2, "action": {"type": "invoke", "field": "f", "args": []}},
              {"type": "module", "line": 3, "filename": "a.1.wasm"
             ]}
            """;

        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.SourceFilename).IsEqualTo("a.wast");
            await Assert.That(document.EnumerationComplete).IsFalse();
            await Assert.That(document.CommandCount).IsNull();
            await Assert
                .That(document.Commands.Select(x => x.Type).SequenceEqual(["module", "action"]))
                .IsTrue();
            await Assert.That(document.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(document.Diagnostics[0].Operation).IsEqualTo("enumerate");
            await Assert.That(document.Diagnostics[0].Path).IsEqualTo(PATH);
            await Assert
                .That(document.Diagnostics[0].ExceptionType)
                .StartsWith("System.Text.Json.");
            await Assert.That(document.Diagnostics[0].Message).Contains("6行目");
            await Assert.That(document.Diagnostics[0].Message).Contains("2件");
        }
    }

    [Test]
    public async Task Root後に余分な内容があるJSONを読む_列挙済みcommandを残して総数を未確定にする()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast", "commands": [{"type": "module", "line": 1}]} x
            """;

        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.Commands.Length).IsEqualTo(1);
            await Assert.That(document.EnumerationComplete).IsFalse();
            await Assert.That(document.CommandCount).IsNull();
            await Assert.That(document.Diagnostics.Length).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 必須項目を欠きroot後に余分な内容があるJSONを読む_欠落と構文破損の両方を残す()
    {
        // Arrange
        const string JSON = """
            {"commands": []} x
            """;

        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.EnumerationComplete).IsFalse();
            await Assert.That(document.Diagnostics.Length).IsEqualTo(2);
            await Assert.That(document.Diagnostics[0].Message).Contains("source_filename");
            await Assert.That(document.Diagnostics[0].ExceptionType).IsNull();
            await Assert
                .That(document.Diagnostics[1].ExceptionType)
                .StartsWith("System.Text.Json.");
        }
    }

    [Test]
    public async Task 境界を確定できるが構造が不正なcommandを読む_1件ずつ残し取得できない項目をnullにする()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast",
             "commands": [
              42,
              {"type": 1, "line": "2", "filename": null},
              {"type": "module", "line": 3, "line": 4, "filename": "a.0.wasm", "filename": "a.1.wasm"},
              {"type": "module", "line": 0, "module_type": ["binary"]},
              {"type": "action", "line": 5}]}
            """;

        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.EnumerationComplete).IsTrue();
            await Assert.That(document.CommandCount).IsEqualTo(5);
            await Assert.That(document.Diagnostics).IsEmpty();
            await Assert
                .That(
                    document
                        .Commands.Select(x => (x.Index, x.Line, x.Type, x.ModuleType, x.Filename))
                        .SequenceEqual([
                            (0, null, null, null, null),
                            (1, null, null, null, null),
                            (2, null, "module", null, null),
                            (3, null, "module", null, null),
                            (4, 5, "action", null, null),
                        ])
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 不正なUTF_8の値を持つcommandを読む_境界を保ち取得できない項目をnullにする()
    {
        // Arrange
        byte[] json =
        [
            .. Encoding.UTF8.GetBytes(
                "{\"source_filename\": \"a.wast\", \"commands\": [{\"type\": \"module\", \"line\": 1, \"filename\": \""
            ),
            0xFF,
            .. Encoding.UTF8.GetBytes("\"}, {\"type\": \"action\", \"line\": 2}]}"),
        ];

        // Act
        var document = ScriptDocument.Parse(json, PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.EnumerationComplete).IsTrue();
            await Assert.That(document.Commands.Length).IsEqualTo(2);
            await Assert.That(document.Commands[0].Type).IsEqualTo("module");
            await Assert.That(document.Commands[0].Filename).IsNull();
            await Assert.That(document.Commands[1].Type).IsEqualTo("action");
        }
    }

    [Test]
    public async Task 対になるサロゲートを欠く項目名を持つJSONを読む_どの項目名とも一致しないものとして列挙を完了する()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast", "\uD800ab": 1,
             "commands": [{"type": "module", "\uD800": 1, "line": 1}]}
            """;

        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.SourceFilename).IsEqualTo("a.wast");
            await Assert.That(document.EnumerationComplete).IsTrue();
            await Assert.That(document.CommandCount).IsEqualTo(1);
            await Assert
                .That(
                    document
                        .Commands.Select(x => (x.Index, x.Line, x.Type, x.ModuleType, x.Filename))
                        .SequenceEqual([(0, 1, "module", null, null)])
                )
                .IsTrue();
            await Assert.That(document.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(document.Diagnostics[0].Message).Contains("固定形式にない");
            await Assert.That(document.Diagnostics[0].ExceptionType).IsNull();
        }
    }

    [Test]
    [Arguments("", "System.Text.Json.")]
    [Arguments("[]", "root")]
    [Arguments("""{"source_filename": "a.wast"}""", "commands")]
    [Arguments("""{"source_filename": "a.wast", "commands": {}}""", "commands")]
    [Arguments(
        """{"source_filename": "a.wast", "commands": [{"type": "module"}], "commands": []}""",
        "commands"
    )]
    public async Task Commandsを一度だけ列挙できないJSONを読む_総数を未確定にして理由を残す(
        string json,
        string expected
    )
    {
        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(json), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.EnumerationComplete).IsFalse();
            await Assert.That(document.CommandCount).IsNull();
            await Assert.That(document.Diagnostics).IsNotEmpty();
            await Assert
                .That(document.Diagnostics.All(x => x.Operation == "enumerate" && x.Path == PATH))
                .IsTrue();
            await Assert
                .That(
                    document.Diagnostics.Any(x =>
                        x.Message.Contains(expected, StringComparison.Ordinal)
                        || x.ExceptionType?.StartsWith(expected, StringComparison.Ordinal) == true
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("""{"commands": []}""", "source_filename")]
    [Arguments("""{"source_filename": 1, "commands": []}""", "source_filename")]
    [Arguments(
        """{"source_filename": "a.wast", "source_filename": "b.wast", "commands": []}""",
        "source_filename"
    )]
    [Arguments("""{"source_filename": "a.wast", "commands": [], "extra": 1}""", "extra")]
    public async Task 全体の項目が固定形式と異なるJSONを読む_列挙を完了したまま理由を残す(
        string json,
        string expected
    )
    {
        // Act
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(json), PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(document.EnumerationComplete).IsTrue();
            await Assert.That(document.CommandCount).IsEqualTo(0);
            await Assert.That(document.Diagnostics.Length).IsEqualTo(1);
            await Assert.That(document.Diagnostics[0].Message).Contains(expected);
            await Assert.That(document.Diagnostics[0].ExceptionType).IsNull();
        }
    }

    [Test]
    public async Task 読取後に入力バッファを変更する_所有した元バイト列を変えない()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast", "commands": [{"type": "module", "line": 1}]}
            """;
        var buffer = Encoding.UTF8.GetBytes(JSON);
        var document = ScriptDocument.Parse(buffer, PATH);

        // Act
        Array.Fill(buffer, (byte)' ');

        // Assert
        await Assert.That(Encoding.UTF8.GetString(document.Content.AsSpan())).IsEqualTo(JSON);
    }
}
