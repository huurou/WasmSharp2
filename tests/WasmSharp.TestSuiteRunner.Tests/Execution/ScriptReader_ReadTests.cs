using System.Collections.Immutable;
using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptReader_ReadTests
{
    private const string INPUT_PATH = "a.wast";

    [Test]
    public async Task 通常moduleとregisterを読む_識別子と登録名を分けmodule_typeの省略をbinaryとする()
    {
        // Arrange
        var document = Parse(
            """
            {"type": "module", "line": 1, "filename": "a.0.wasm"},
            {"type": "module", "line": 2, "name": "$M", "filename": "a.1.wasm"},
            {"type": "register", "line": 3, "name": "$M", "as": "M"},
            {"type": "register", "line": 4, "as": "N"}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.InputPath).IsEqualTo(INPUT_PATH);
            await Assert.That(result.EnumerationComplete).IsTrue();
            await Assert.That(result.Commands.Length).IsEqualTo(4);
            await Assert
                .That(result.Commands[0])
                .IsEqualTo(new ModuleCommand(0, 1, null, "a.0.wasm", ScriptModuleType.Binary));
            await Assert
                .That(result.Commands[1])
                .IsEqualTo(new ModuleCommand(1, 2, "$M", "a.1.wasm", ScriptModuleType.Binary));
            await Assert.That(result.Commands[2]).IsEqualTo(new RegisterCommand(2, 3, "$M", "M"));
            await Assert.That(result.Commands[3]).IsEqualTo(new RegisterCommand(3, 4, null, "N"));
            await Assert
                .That(
                    result
                        .Commands.Select(x => (x.Type, x.Category))
                        .SequenceEqual([
                            ("module", CaseCategory.Setup),
                            ("module", CaseCategory.Setup),
                            ("register", CaseCategory.Setup),
                            ("register", CaseCategory.Setup),
                        ])
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments(WasmSpecVersion.Core2)]
    [Arguments(WasmSpecVersion.Core3)]
    public async Task テキスト素材にbinary_filenameを併記する_wasmTools形式なら元のテキスト素材を保持する(
        WasmSpecVersion version
    )
    {
        // Arrange
        const string Commands = """
            {"type":"module","line":1,"filename":"a.wat","module_type":"text","binary_filename":"a.wasm"},
            {"type":"assert_invalid","line":2,"filename":"b.wat","module_type":"text","text":"invalid","binary_filename":"b.wasm"},
            {"type":"module","line":3,"filename":"c.wasm","module_type":"binary","binary_filename":"other.wasm"}
            """;
        var wasmTools = Parse(Commands, wasmTools: true);
        var wabt = Parse(Commands);

        // Act
        var result = ScriptReader.Read(wasmTools, INPUT_PATH, version);
        var legacy = ScriptReader.Read(wabt, INPUT_PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Commands[0]).IsEqualTo(new ModuleCommand(0, 1, null, "a.wat", ScriptModuleType.Text));
            await Assert
                .That(result.Commands[1])
                .IsEqualTo(new AssertInvalidCommand(1, 2, "b.wat", "invalid", ScriptModuleType.Text));
            await Assert.That(result.Commands[2]).IsTypeOf<InvalidCommand>();
            await Assert
                .That(legacy.Commands.OfType<InvalidCommand>().Select(x => x.Diagnostic.Message))
                .IsEquivalentTo(Enumerable.Repeat("固定形式にないbinary_filenameがあります。", 3));
        }
    }

    [Test]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"i31ref","value":"7"}]}}""",
        "固定形式にないaction.args[0].valueがあります。"
    )]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"funcref"}]}}""",
        "action.args[0].valueがありません。"
    )]
    [Arguments("""{"type":"module_instance","line":1,"extra":0}""", "固定形式にないextraがあります。")]
    [Arguments("""{"type":"module_instance","line":1,"instance":3}""", "instanceが文字列ではありません。")]
    [Arguments("""{"type":"module_instance","line":1,"module":3}""", "moduleが文字列ではありません。")]
    [Arguments(
        """{"type":"assert_exception","line":1,"action":{"type":"get","field":"g"},"text":"unexpected"}""",
        "固定形式にないtextがあります。"
    )]
    [Arguments(
        """{"type":"assert_return","line":1,"action":{"type":"get","field":"g"},"expected":[{"type":"either","values":[{"type":"i32","value":"1"}],"extra":0}]}""",
        "固定形式にないexpected[0].extraがあります。"
    )]
    [Arguments(
        """{"type":"assert_return","line":1,"action":{"type":"get","field":"g"},"expected":[{"type":"refnull","extra":0}]}""",
        "固定形式にないexpected[0].extraがあります。"
    )]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"anyref"}]}}""",
        "action.args[0].valueがありません。"
    )]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"refnull","value":"null"}]}}""",
        "action.args[0].typeのrefnullは固定形式にない値です。"
    )]
    [Arguments(
        """{"type":"module","line":1,"filename":"a.wat","module_type":3,"binary_filename":"a.wasm"}""",
        "module_typeが文字列ではありません。"
    )]
    [Arguments(
        """{"type":"module","line":1,"filename":"a.wat","module_type":"text","binary_filename":3}""",
        "binary_filenameが文字列ではありません。"
    )]
    public async Task Core3のcommandが固定形式に合わない_問題の項目と理由を診断する(string json, string message)
    {
        // Arrange
        var document = Parse(json, wasmTools: true);

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH, WasmSpecVersion.Core3);

        // Assert
        var invalid = (InvalidCommand)result.Commands.Single();
        await Assert.That(invalid.Diagnostic.Message).IsEqualTo(message);
    }

    [Test]
    [Arguments("binary", ScriptModuleType.Binary)]
    [Arguments("text", ScriptModuleType.Text)]
    public async Task 通常moduleがmodule_typeを持つ_指定された形式を保持する(string moduleType, ScriptModuleType expected)
    {
        // Arrange
        var document = Parse($$"""{"type": "module", "line": 1, "filename": "a.0.wasm", "module_type": "{{moduleType}}"}""");

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        await Assert.That(result.Commands[0]).IsEqualTo(new ModuleCommand(0, 1, null, "a.0.wasm", expected));
    }

    [Test]
    [Arguments(WasmSpecVersion.Core2)]
    [Arguments(WasmSpecVersion.Core3)]
    public async Task 単独actionを読む_invokeとgetを区別し型だけの結果宣言を保持する(WasmSpecVersion version)
    {
        // Arrange
        var document = Parse(
            """
            {"type": "action", "line": 1, "action": {"type": "invoke", "module": "$M", "field": "f", "args": [{"type": "i32", "value": "7"}]}, "expected": [{"type": "i32"}, {"type": "i64"}, {"type": "f32"}, {"type": "f64"}, {"type": "funcref"}, {"type": "externref"}]},
            {"type": "action", "line": 2, "action": {"type": "get", "module": "$N", "field": "g"}, "expected": [{"type": "v128"}]},
            {"type": "action", "line": 3, "action": {"type": "invoke", "field": "h", "args": []}, "expected": []}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH, version);

        // Assert
        var invoke = (ActionCommand)result.Commands[0];
        var get = (ActionCommand)result.Commands[1];
        var empty = (ActionCommand)result.Commands[2];
        using (Assert.Multiple())
        {
            await Assert.That(result.Commands.All(x => x is ActionCommand)).IsTrue();
            await Assert.That(invoke.Line).IsEqualTo(1);
            await Assert.That(invoke.Category).IsEqualTo(CaseCategory.Action);
            await Assert.That(FormatAction(invoke.Action)).IsEqualTo("invoke $M f [i32:7]");
            await Assert
                .That(
                    invoke.ResultTypes.SequenceEqual([
                        WasmValueKind.I32,
                        WasmValueKind.I64,
                        WasmValueKind.F32,
                        WasmValueKind.F64,
                        WasmValueKind.FuncRef,
                        WasmValueKind.ExternRef,
                    ])
                )
                .IsTrue();
            await Assert.That(FormatAction(get.Action)).IsEqualTo("get $N g");
            await Assert.That(get.ResultTypes.SequenceEqual([WasmValueKind.V128])).IsTrue();
            await Assert.That(FormatAction(empty.Action)).IsEqualTo("invoke - h []");
            await Assert.That(empty.ResultTypes).IsEmpty();
        }
    }

    [Test]
    public async Task Assert_returnを読む_全値型の引数と値付き期待値の文字列を加工せず分けて保持する()
    {
        // Arrange
        var document = Parse(
            """
            {"type": "assert_return", "line": 5, "action": {"type": "invoke", "field": "f", "args": [
              {"type": "i32", "value": "4294967295"},
              {"type": "i64", "value": "18446744073709551615"},
              {"type": "f32", "value": "2143289345"},
              {"type": "f64", "value": "9221120237041090561"},
              {"type": "v128", "lane_type": "i32", "value": ["0", "1", "2", "4294967295"]},
              {"type": "funcref", "value": "null"},
              {"type": "externref", "value": "null"},
              {"type": "externref", "value": "1"}]},
             "expected": [
              {"type": "i32", "value": "0"},
              {"type": "i64", "value": "9223372036854775808"},
              {"type": "f32", "value": "nan:canonical"},
              {"type": "f64", "value": "nan:arithmetic"},
              {"type": "v128", "lane_type": "f32", "value": ["0", "nan:arithmetic", "2147483648", "nan:canonical"]},
              {"type": "funcref", "value": "null"},
              {"type": "externref", "value": "1"}]}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = (AssertReturnCommand)result.Commands[0];
        using (Assert.Multiple())
        {
            await Assert.That(command.Line).IsEqualTo(5);
            await Assert.That(command.Category).IsEqualTo(CaseCategory.Assertion);
            await Assert
                .That(FormatAction(command.Action))
                .IsEqualTo(
                    "invoke - f [i32:4294967295, i64:18446744073709551615, f32:2143289345, f64:9221120237041090561, v128:i32(0,1,2,4294967295), funcref:null, externref:null, externref:1]"
                );
            await Assert
                .That(
                    command
                        .Expected.Cast<ExpectedValue>()
                        .Select(Format)
                        .SequenceEqual([
                            "i32:0",
                            "i64:9223372036854775808",
                            "f32:nan:canonical",
                            "f64:nan:arithmetic",
                            "v128:f32(0,nan:arithmetic,2147483648,nan:canonical)",
                            "funcref:null",
                            "externref:1",
                        ])
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments("i8", LaneType.I8)]
    [Arguments("i16", LaneType.I16)]
    [Arguments("i32", LaneType.I32)]
    [Arguments("i64", LaneType.I64)]
    [Arguments("f32", LaneType.F32)]
    [Arguments("f64", LaneType.F64)]
    public async Task V128のlane型を読む_lane型とlane0から順の文字列を保持する(string laneType, LaneType expected)
    {
        // Arrange
        var document = Parse(
            $$"""
            {"type": "assert_return", "line": 1, "action": {"type": "invoke", "field": "f", "args": [{"type": "v128", "lane_type": "{{laneType}}", "value": ["1", "2"]}]},
             "expected": [{"type": "v128", "lane_type": "{{laneType}}", "value": ["3", "4"]}]}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = (AssertReturnCommand)result.Commands[0];
        var argument = ((InvokeAction)command.Action).Arguments[0];
        var expectedValue = (ExpectedValue)command.Expected[0];
        using (Assert.Multiple())
        {
            await Assert.That(argument.Type).IsEqualTo("v128");
            await Assert.That(argument.LaneType).IsEqualTo(expected);
            await Assert.That(argument.Value).IsNull();
            await Assert.That(argument.Lanes.SequenceEqual(["1", "2"])).IsTrue();
            await Assert.That(expectedValue.Type).IsEqualTo("v128");
            await Assert.That(expectedValue.LaneType).IsEqualTo(expected);
            await Assert.That(expectedValue.Lanes.SequenceEqual(["3", "4"])).IsTrue();
        }
    }

    [Test]
    public async Task Assert_trapとassert_exhaustionを読む_期待診断と型だけの結果宣言を保持する()
    {
        // Arrange
        var document = Parse(
            """
            {"type": "assert_trap", "line": 1, "action": {"type": "invoke", "field": "f", "args": []}, "text": "integer divide by zero", "expected": [{"type": "i32"}]},
            {"type": "assert_exhaustion", "line": 2, "action": {"type": "invoke", "module": "$M", "field": "g", "args": []}, "text": "call stack exhausted", "expected": []}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var trap = (AssertTrapCommand)result.Commands[0];
        var exhaustion = (AssertExhaustionCommand)result.Commands[1];
        using (Assert.Multiple())
        {
            await Assert.That(trap.Text).IsEqualTo("integer divide by zero");
            await Assert.That(FormatAction(trap.Action)).IsEqualTo("invoke - f []");
            await Assert.That(trap.ResultTypes.SequenceEqual([WasmValueKind.I32])).IsTrue();
            await Assert.That(trap.Category).IsEqualTo(CaseCategory.Assertion);
            await Assert.That(exhaustion.Text).IsEqualTo("call stack exhausted");
            await Assert.That(FormatAction(exhaustion.Action)).IsEqualTo("invoke $M g []");
            await Assert.That(exhaustion.ResultTypes).IsEmpty();
            await Assert.That(exhaustion.Category).IsEqualTo(CaseCategory.Assertion);
        }
    }

    [Test]
    [Arguments("assert_trap")]
    [Arguments("assert_exhaustion")]
    public async Task 否定actionでexpectedを省略する_wasmTools形式は空の結果型を返しWABT形式は項目の不足を報告する(
        string commandType
    )
    {
        // Arrange
        var commands =
            $$"""{"type":"{{commandType}}","line":1,"action":{"type":"invoke","field":"f","args":[]},"text":"failure"}""";
        var wasmTools = Parse(commands, wasmTools: true);
        var wabt = Parse(commands);

        // Act
        var command = ScriptReader.Read(wasmTools, INPUT_PATH).Commands.Single();
        var legacy = ScriptReader.Read(wabt, INPUT_PATH).Commands.Single();

        // Assert
        var resultTypes =
            commandType == "assert_trap"
                ? (await Assert.That(command).IsTypeOf<AssertTrapCommand>())!.ResultTypes
                : (await Assert.That(command).IsTypeOf<AssertExhaustionCommand>())!.ResultTypes;
        var invalid = await Assert.That(legacy).IsTypeOf<InvalidCommand>();
        using (Assert.Multiple())
        {
            await Assert.That(resultTypes).IsEmpty();
            await Assert.That(invalid!.Diagnostic.Message).IsEqualTo("expectedがありません。");
        }
    }

    [Test]
    public async Task 否定module_assertionを読む_種類別に素材と期待診断と形式を保持する()
    {
        // Arrange
        var document = Parse(
            """
            {"type": "assert_malformed", "line": 1, "filename": "a.0.wat", "text": "unexpected token", "module_type": "text"},
            {"type": "assert_malformed", "line": 2, "filename": "a.1.wasm", "text": "magic header not detected", "module_type": "binary"},
            {"type": "assert_invalid", "line": 3, "filename": "a.2.wasm", "text": "type mismatch", "module_type": "binary"},
            {"type": "assert_unlinkable", "line": 4, "filename": "a.3.wasm", "text": "unknown import", "module_type": "binary"},
            {"type": "assert_uninstantiable", "line": 5, "filename": "a.4.wasm", "text": "unreachable", "module_type": "binary"}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(result.Commands[0])
                .IsEqualTo(new AssertMalformedCommand(0, 1, "a.0.wat", "unexpected token", ScriptModuleType.Text));
            await Assert
                .That(result.Commands[1])
                .IsEqualTo(
                    new AssertMalformedCommand(1, 2, "a.1.wasm", "magic header not detected", ScriptModuleType.Binary)
                );
            await Assert
                .That(result.Commands[2])
                .IsEqualTo(new AssertInvalidCommand(2, 3, "a.2.wasm", "type mismatch", ScriptModuleType.Binary));
            await Assert
                .That(result.Commands[3])
                .IsEqualTo(new AssertUnlinkableCommand(3, 4, "a.3.wasm", "unknown import", ScriptModuleType.Binary));
            await Assert
                .That(result.Commands[4])
                .IsEqualTo(new AssertUninstantiableCommand(4, 5, "a.4.wasm", "unreachable", ScriptModuleType.Binary));
            await Assert.That(result.Commands.All(x => x.Category == CaseCategory.Assertion)).IsTrue();
        }
    }

    [Test]
    public async Task 境界が確定した不正要素を含む_1件ずつInvalidCommandとして残し後続を読み続ける()
    {
        // Arrange
        var document = Parse(
            """
            {"type": "module", "line": 1, "filename": "a.0.wasm"},
            1,
            {"type": "assert_exception", "line": 3, "action": {"type": "invoke", "field": "f", "args": []}, "expected": []},
            {"type": "register", "line": 4, "as": "M"}
            """
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var notObject = (InvalidCommand)result.Commands[1];
        var unknown = (InvalidCommand)result.Commands[2];
        using (Assert.Multiple())
        {
            await Assert.That(result.EnumerationComplete).IsTrue();
            await Assert.That(result.Commands.Length).IsEqualTo(4);
            await Assert.That(result.Commands[0]).IsTypeOf<ModuleCommand>();
            await Assert.That(notObject.Index).IsEqualTo(1);
            await Assert.That(notObject.Line).IsNull();
            await Assert.That(notObject.Type).IsNull();
            await Assert.That(notObject.Category).IsNull();
            await Assert.That(notObject.Diagnostic.Operation).IsEqualTo("read_command");
            await Assert.That(notObject.Diagnostic.SourceJson).IsEqualTo("1");
            await Assert.That(unknown.Index).IsEqualTo(2);
            await Assert.That(unknown.Line).IsEqualTo(3);
            await Assert.That(unknown.Type).IsEqualTo("assert_exception");
            await Assert.That(unknown.Category).IsNull();
            await Assert.That(unknown.Diagnostic.Message).Contains("assert_exception");
            await Assert.That(unknown.Diagnostic.SourceJson).StartsWith("""{"type": "assert_exception",""");
            await Assert.That(result.Commands[3]).IsEqualTo(new RegisterCommand(3, 4, null, "M"));
        }
    }

    [Test]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}}""",
        "expected"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "either": [{"type": "i32", "value": "1"}]}""",
        "either"
    )]
    [Arguments(
        """{"type":"assert_return","line":7,"action":{"type":"get","field":"g"},"expected":[{"type":"either","values":[{"type":"i32","value":"1"}]}]}""",
        "either"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "i32"}]}""",
        "expected[0].value"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": [{"type": "exnref", "value": "null"}]}, "expected": []}""",
        "exnref"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": [{"type": "F32", "value": "0"}]}, "expected": []}""",
        "F32"
    )]
    [Arguments(
        """{"type":"action","line":7,"action":{"type":"invoke","field":"f","args":[{"type":"i16","value":"0"}]},"expected":[]}""",
        "action.args[0].typeのi16は固定形式にない値です。"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": [{"type": "i32", "value": 1}]}, "expected": []}""",
        "action.args[0].value"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": [{"type": "v128", "value": ["0", "0"]}]}, "expected": []}""",
        "action.args[0].lane_type"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "v128", "lane_type": "i128", "value": ["0"]}]}""",
        "i128"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "v128", "lane_type": "i8", "value": "0"}]}""",
        "expected[0].value"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "v128", "lane_type": "i8", "value": [0]}]}""",
        "expected[0].value[0]"
    )]
    [Arguments(
        """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "i32", "lane_type": "i8", "value": "0"}]}""",
        "expected[0].lane_type"
    )]
    [Arguments(
        """{"type": "action", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": [{"type": "i32", "value": "1"}]}""",
        "expected[0].value"
    )]
    [Arguments(
        """{"type": "action", "line": 7, "action": {"type": "call", "field": "f", "args": []}, "expected": []}""",
        "call"
    )]
    [Arguments(
        """{"type": "action", "line": 7, "action": {"type": "invoke", "field": "f"}, "expected": []}""",
        "action.args"
    )]
    [Arguments(
        """{"type": "action", "line": 7, "action": {"type": "get", "field": "g", "args": []}, "expected": []}""",
        "action.args"
    )]
    [Arguments(
        """{"type": "action", "line": 7, "action": {"type": "invoke", "args": []}, "expected": []}""",
        "action.field"
    )]
    [Arguments("""{"type": "action", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}}""", "expected")]
    [Arguments(
        """{"type": "assert_trap", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}, "expected": []}""",
        "text"
    )]
    [Arguments("""{"type": "assert_invalid", "line": 7, "filename": "a.0.wasm", "text": "type mismatch"}""", "module_type")]
    [Arguments(
        """{"type": "assert_invalid", "line": 7, "filename": "a.0.wasm", "text": "type mismatch", "module_type": "quote"}""",
        "quote"
    )]
    [Arguments("""{"type": "module", "line": 7, "filename": "a.0.wasm", "name": "$A", "extra": true}""", "extra")]
    [Arguments("""{"type": "module", "line": 7, "filename": "a.0.wasm", "filename": "a.1.wasm"}""", "filename")]
    [Arguments(
        """{"type":"action","line":7,"action":{"type":"invoke","field":"f","args":[{"type":"i31ref"}]},"expected":[]}""",
        "action.args[0].typeのi31refは固定形式にない値です。"
    )]
    [Arguments("""{"type": "register", "line": 7}""", "as")]
    [Arguments("""{"type": "module", "line": 0, "filename": "a.0.wasm"}""", "line")]
    [Arguments("""{"type": "module", "filename": "a.0.wasm"}""", "line")]
    [Arguments("""{"line": 7, "filename": "a.0.wasm"}""", "type")]
    public async Task 固定形式の構造や値を満たさないcommandを読む_位置と元JSONを持つInvalidCommandにする(
        string json,
        string reason
    )
    {
        // Arrange
        var document = Parse(json);

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = await Assert.That(result.Commands[0]).IsTypeOf<InvalidCommand>();
        using (Assert.Multiple())
        {
            await Assert.That(command!.Index).IsEqualTo(0);
            await Assert.That(command.Diagnostic.Operation).IsEqualTo("read_command");
            await Assert.That(command.Diagnostic.Message).Contains(reason);
            await Assert.That(command.Diagnostic.SourceJson).IsEqualTo(json);
        }
    }

    [Test]
    public async Task 必須値が不足したassertionを読む_取得できた行と種類と集計区分を残す()
    {
        // Arrange
        var document = Parse(
            """{"type": "assert_return", "line": 7, "action": {"type": "invoke", "field": "f", "args": []}}"""
        );

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = (InvalidCommand)result.Commands[0];
        using (Assert.Multiple())
        {
            await Assert.That(command.Line).IsEqualTo(7);
            await Assert.That(command.Type).IsEqualTo("assert_return");
            await Assert.That(command.Category).IsEqualTo(CaseCategory.Assertion);
        }
    }

    [Test]
    [Arguments("""{"type": "module", "line": 1, "name": "$M"}""", "$M", null)]
    [Arguments("""{"type":"module","line":1,"name":"$M","\uD800abc":"$other","filename":"a.0.wasm"}""", "$M", null)]
    [Arguments("""{"type": "module", "line": 1, "name": 1, "filename": "a.0.wasm", "x": 0}""", null, null)]
    [Arguments("""{"type":"module","line":1,"name":"\uD800","filename":"a.0.wasm"}""", null, null)]
    [Arguments("""{"type": "module", "line": 1, "name": "$A", "name": "$B", "filename": "a.0.wasm"}""", null, null)]
    [Arguments("""{"type": "register", "line": 2, "name": "$M", "as": "M", "x": 0}""", null, "M")]
    [Arguments("""{"type": "register", "line": 2, "as": ["M"]}""", null, null)]
    [Arguments("""{"type":"register","line":2,"as":"\uD800"}""", null, null)]
    [Arguments("""{"type": "assert_invalid", "line": 3, "name": "$M", "as": "M", "filename": "a.0.wasm"}""", null, null)]
    public async Task 不正なmoduleとregisterを読む_読み取れた更新対象の名前だけを保持する(
        string json,
        string? name,
        string? registeredName
    )
    {
        // Arrange
        var document = Parse(json);

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = (InvalidCommand)result.Commands[0];
        using (Assert.Multiple())
        {
            await Assert.That(command.Name).IsEqualTo(name);
            await Assert.That(command.As).IsEqualTo(registeredName);
            await Assert.That(command.Diagnostic.Operation).IsEqualTo("read_command");
            await Assert.That(command.Diagnostic.SourceJson).IsEqualTo(json);
        }
    }

    [Test]
    public async Task 途中で構文が破損したJSONを読む_確定済みcommandだけを読み列挙未完了を保持する()
    {
        // Arrange
        const string JSON = """
            {"source_filename": "a.wast",
             "commands": [
              {"type": "module", "line": 1, "filename": "a.0.wasm"},
              {"type": "action", "line": 2, "action": {"type": "get", "field": "g"}, "expected": [{"type": "i32"}]},
              {"type": "module", "line": 3, "filename": "a.1.wasm"
             ]}
            """;
        var document = ScriptDocument.Parse(Encoding.UTF8.GetBytes(JSON), "modules/a.json");

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.EnumerationComplete).IsFalse();
            await Assert.That(result.Commands.Length).IsEqualTo(2);
            await Assert.That(result.Commands[0]).IsTypeOf<ModuleCommand>();
            await Assert.That(result.Commands[1]).IsTypeOf<ActionCommand>();
        }
    }

    [Test]
    [Arguments("""{"type":"module","line":1,"filename":"a.0.wasm","extra":0}""", "extra")]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[],"extra":0}""",
        "extra"
    )]
    [Arguments(
        """{"type":"assert_return","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[],"extra":0}""",
        "extra"
    )]
    [Arguments(
        """{"type":"assert_trap","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[],"text":"trap","extra":0}""",
        "extra"
    )]
    [Arguments(
        """{"type":"assert_invalid","line":1,"filename":"a.0.wasm","text":"invalid","module_type":"binary","extra":0}""",
        "extra"
    )]
    [Arguments("""{"type":"register","line":1,"as":"M","extra":0}""", "extra")]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[],"extra":0},"expected":[]}""",
        "action.extra"
    )]
    [Arguments(
        """{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"v128","lane_type":"i32","value":["1","2","3","4"],"extra":0}]},"expected":[]}""",
        "action.args[0].extra"
    )]
    public async Task 固定形式にない項目を各階層に含む_項目の位置を診断してcommandを拒否する(string json, string path)
    {
        // Arrange
        var document = Parse(json);

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = await Assert.That(result.Commands[0]).IsTypeOf<InvalidCommand>();
        await Assert.That(command!.Diagnostic.Message).IsEqualTo($"固定形式にない{path}があります。");
    }

    [Test]
    [Arguments("""{"type":"action","line":1,"action":null,"expected":[]}""", "actionがobjectではありません。")]
    [Arguments("""{"line":1}""", "typeがありません。")]
    [Arguments("""{"type":"module","line":1}""", "filenameがありません。")]
    [Arguments("""{"type":"module","line":1,"name":null,"filename":"a.0.wasm"}""", "nameが文字列ではありません。")]
    [Arguments(
        """{"type":"module","line":1,"filename":"a.0.wasm","module_type":null}""",
        "module_typeが文字列ではありません。"
    )]
    [Arguments("""{"type":"register","line":1,"name":null,"as":"M"}""", "nameが文字列ではありません。")]
    [Arguments("""{"type":"register","line":1}""", "asがありません。")]
    [Arguments("""{"type":"action","line":1,"expected":[]}""", "actionがありません。")]
    [Arguments("""{"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]}}""", "expectedがありません。")]
    [Arguments(
        """{"type":"assert_return","line":1,"action":{"type":"invoke","field":"f","args":[]}}""",
        "expectedがありません。"
    )]
    [Arguments(
        """{"type":"assert_trap","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[]}""",
        "textがありません。"
    )]
    [Arguments("""{"type":"assert_invalid","line":1,"text":"invalid","module_type":"binary"}""", "filenameがありません。")]
    [Arguments("""{"type":"assert_invalid","line":1,"filename":"a.0.wasm","module_type":"binary"}""", "textがありません。")]
    [Arguments("""{"type":"assert_invalid","line":1,"filename":"a.0.wasm","text":"invalid"}""", "module_typeがありません。")]
    [Arguments("""{"type":"module","filename":"a.0.wasm"}""", "lineがありません。")]
    [Arguments("""{"type":"module","line":1,"filename":"\uD800"}""", "filenameを文字列として取得できません。")]
    [Arguments("""{"type":"module","line":1,"name":"\uD800","filename":"a.0.wasm"}""", "nameを文字列として取得できません。")]
    [Arguments("""{"type":"register","line":1,"as":"\uD800"}""", "asを文字列として取得できません。")]
    [Arguments(
        """{"type":"module","line":1,"filename":"a.0.wasm","\uD800":0}""",
        "commandに文字列として取得できない項目名があります。"
    )]
    public async Task 項目が欠落または取得できないcommandを読む_ルートからの正確な位置と理由を残す(
        string json,
        string message
    )
    {
        // Arrange
        var document = Parse(json);

        // Act
        var result = ScriptReader.Read(document, INPUT_PATH);

        // Assert
        var command = await Assert.That(result.Commands[0]).IsTypeOf<InvalidCommand>();
        await Assert.That(command!.Diagnostic.Message).IsEqualTo(message);
    }

    private static ScriptDocument Parse(string commands, bool wasmTools = false)
    {
        var json = $$"""
            {"source_filename": "a.wast",
             "commands": [
            {{commands}}
            ]}
            """;
        return ScriptDocument.Parse(Encoding.UTF8.GetBytes(json), "modules/a.json", wasmTools);
    }

    private static string FormatAction(ScriptAction action)
    {
        return action switch
        {
            InvokeAction x =>
                $"invoke {x.Module ?? "-"} {x.Field} [{string.Join(", ", x.Arguments.Select(y => Format(y.Type, y.Value, y.LaneType, y.Lanes)))}]",
            GetAction x => $"get {x.Module ?? "-"} {x.Field}",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static string Format(ExpectedValue value)
    {
        return Format(value.Type, value.Value, value.LaneType, value.Lanes);
    }

    private static string Format(string type, string? value, LaneType? laneType, ImmutableArray<string> lanes)
    {
        return laneType is { } lane
            ? $"{type}:{lane.ToString().ToLowerInvariant()}({string.Join(",", lanes)}){value}"
            : $"{type}:{value}{string.Join(",", lanes)}";
    }
}
