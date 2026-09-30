using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public partial class ScriptExecutor_ExecuteTests
{
    [Test]
    public async Task 同名の再登録_古いexportと失敗前の成功へ戻らない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            ScriptExecutionFixture.Module(0, "$A")
            + ","
            + """
                {"type":"register","line":1,"as":"shared"},
                """
            + ScriptExecutionFixture.Module(2)
            + ","
            + """
                {"type":"register","line":1,"as":"shared"},
                """
            + ScriptExecutionFixture.Module(4)
            + ","
            + """
                {"type":"register","line":1,"name":"$missing","as":"shared"},
                """
            + ScriptExecutionFixture.Module(6);

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Exports()),
            (2, ScriptExecutionFixture.Empty),
            (4, ScriptExecutionFixture.ImportExports("shared")),
            (6, ScriptExecutionFixture.ImportExports("shared"))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[4].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(results[4].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(results[6].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(results[6].Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 5)]);
            await Assert.That(results[5].LastStage).IsNull();
            await Assert.That(results[5].Diagnostics[0].Operation).IsEqualTo("resolve_module");
            await Assert.That(state.ResolveModule("$A")!.Value).IsNotNull();
            await Assert.That(state.LastModule!.Value).IsNull();
        }
    }

    [Test]
    public async Task 同名moduleの失敗_直近と識別子を利用不能に更新する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            ScriptExecutionFixture.Module(0, "$A")
            + ","
            + ScriptExecutionFixture.Module(1, "$A")
            + ","
            + """
                {"type":"register","line":1,"name":"$A","as":"named"},
                {"type":"register","line":1,"as":"last"}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Empty),
            (1, [1])
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[2].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(results[3].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(results[2].LastStage).IsNull();
            await Assert.That(results[3].LastStage).IsNull();
            await Assert.That(results[2].Cause!.Origins).IsEquivalentTo([new CaseId("a.wast", 1)]);
            await Assert.That(state.ResolveModule("$A")!.Value).IsNull();
        }
    }

    [Test]
    public async Task 否定moduleのstartが共有globalを書き換えてtrap_名前状態を変えず副作用を保持する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var global = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var host = new WasmHostModule("shared");
        host.Define("g", global);
        state.Register(host);
        var start = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (
                2,
                [
                    1,
                    .. ScriptExecutionFixture.Name("shared"),
                    .. ScriptExecutionFixture.Name("g"),
                    3,
                    0x7F,
                    1,
                ]
            ),
            (3, [1, 0]),
            (8, [0]),
            (10, [1, 7, 0, 0x41, 7, 0x24, 0, 0x00, 0x0B])
        );
        var commands =
            ScriptExecutionFixture.Module(0, "$A")
            + ","
            + """
                {"type":"assert_uninstantiable","line":1,"filename":"a.1.wasm","module_type":"binary","text":""},
                {"type":"assert_malformed","line":1,"filename":"a.2.wasm","module_type":"binary","text":""},
                {"type":"assert_invalid","line":1,"filename":"a.3.wasm","module_type":"binary","text":""}
                """;
        var invalid = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 1, 0x7F]),
            (3, [1, 0]),
            (10, [1, 2, 0, 0x0B])
        );

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Empty),
            (1, start),
            (2, [1]),
            (3, invalid)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.All(x => x.Outcome == CaseOutcome.Passed)).IsTrue();
            await Assert.That(global.Value.AsI32()).IsEqualTo(7);
            await Assert
                .That(ReferenceEquals(state.LastModule, state.ResolveModule("$A")))
                .IsTrue();
            await Assert.That(state.LastModule!.Cause).IsNull();
            await Assert.That(results[1].LastStage).IsEqualTo(CaseStage.Instantiate);
        }
    }

    [Test]
    public async Task 素材異常の否定assertion_期待失敗へ置き換えない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var input = new WasmSharp.TestSuiteRunner.Corpus.InputVerification(
            new("a.wast", new string('a', 64)),
            null,
            [],
            [],
            System.Collections.Immutable.ImmutableDictionary<
                int,
                WasmSharp.TestSuiteRunner.Corpus.CorpusDiagnostic
            >.Empty.Add(0, new("verify", "hash不一致", "a.0.wasm"))
        );
        var script = new ScriptReadResult(
            "a.wast",
            [new AssertMalformedCommand(0, 1, "a.0.wasm", "", ScriptModuleType.Binary)],
            true
        );

        // Act
        var results = new ScriptExecutor(input).Execute(script, state);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[0].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(results[0].LastStage).IsNull();
            await Assert.That(results[0].Diagnostics[0].Operation).IsEqualTo("verify");
            await Assert.That(results[0].Diagnostics[0].Path).IsEqualTo("a.0.wasm");
        }
    }

    [Test]
    public async Task 通常moduleと登録とimport_全exportの実体を共有する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            ScriptExecutionFixture.Module(0, "$A")
            + ","
            + """{"type":"register","line":1,"name":"$A","as":"shared"},"""
            + ScriptExecutionFixture.Module(2, "$B");

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Exports()),
            (2, ScriptExecutionFixture.ImportExports("shared"))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Length).IsEqualTo(3);
            await Assert.That(results.All(x => x.Outcome == CaseOutcome.Passed)).IsTrue();
            var a = state.ResolveModule("$A")!.Value!.Instance;
            var b = state.ResolveModule("$B")!.Value!.Instance;
            using (Assert.Multiple())
            {
                await Assert.That(ReferenceEquals(a.GetFunction("f"), b.GetFunction("f"))).IsTrue();
                await Assert
                    .That(ReferenceEquals(a.GetGlobalResource("g"), b.GetGlobalResource("g")))
                    .IsTrue();
                await Assert.That(ReferenceEquals(a.GetMemory("m"), b.GetMemory("m"))).IsTrue();
                await Assert.That(ReferenceEquals(a.GetTable("t"), b.GetTable("t"))).IsTrue();
                await Assert.That(a.ExecutionOptions.MaxCallDepth).IsEqualTo(1024);
                await Assert.That(b.ExecutionOptions.MaxCallDepth).IsEqualTo(1024);
            }
        }
    }

    [Test]
    public async Task 複数の失敗登録に依存_直接原因と元の失敗を保持する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands = """
            {"type":"register","line":1,"name":"$missing","as":"one"},
            {"type":"register","line":1,"name":"$missing","as":"two"},
            {"type":"module","line":1,"name":"$M","filename":"a.2.wasm"},
            {"type":"register","line":1,"name":"$M","as":"three"},
            {"type":"module","line":1,"filename":"a.4.wasm"}
            """;

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (2, ScriptExecutionFixture.ImportFunctions("one", "two")),
            (4, ScriptExecutionFixture.ImportFunctions("three"))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Length).IsEqualTo(5);
            await Assert.That(results[2].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(results[2].LastStage).IsEqualTo(CaseStage.InspectImports);
            await Assert
                .That(results[2].Cause!.Direct)
                .IsEquivalentTo([new CaseId("a.wast", 0), new("a.wast", 1)]);
            await Assert.That(results[4].Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 3)]);
            await Assert
                .That(results[4].Cause!.Origins)
                .IsEquivalentTo([new CaseId("a.wast", 0), new("a.wast", 1)]);
        }
    }

    [Test]
    public async Task 失敗登録と不正binary_DecodeとValidateの失敗を先に記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            """{"type":"register","line":1,"as":"one"},"""
            + ScriptExecutionFixture.Module(1)
            + ","
            + ScriptExecutionFixture.Module(2);
        var invalid = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 1, 0x7F]),
            (
                2,
                [
                    1,
                    .. ScriptExecutionFixture.Name("one"),
                    .. ScriptExecutionFixture.Name("f"),
                    0,
                    0,
                ]
            ),
            (3, [1, 0]),
            (10, [1, 2, 0, 0x0B])
        );

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (1, [1]), (2, invalid));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Length).IsEqualTo(3);
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(results[1].LastStage).IsEqualTo(CaseStage.Decode);
            await Assert.That(results[2].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(results[2].LastStage).IsEqualTo(CaseStage.Validate);
        }
    }

    [Test]
    [Arguments("assert_malformed")]
    [Arguments("assert_invalid")]
    [Arguments("assert_unlinkable")]
    [Arguments("assert_uninstantiable")]
    public async Task 失敗登録に依存する否定module_段階ごとに判定し名前状態を変更しない(string type)
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var outcome = type is "assert_malformed" or "assert_invalid"
            ? CaseOutcome.Passed
            : CaseOutcome.Blocked;
        var stage = type switch
        {
            "assert_malformed" => CaseStage.Decode,
            "assert_invalid" => CaseStage.Validate,
            _ => CaseStage.InspectImports,
        };
        var invalid = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 1, 0x7F]),
            (
                2,
                [
                    1,
                    .. ScriptExecutionFixture.Name("failed"),
                    .. ScriptExecutionFixture.Name("f"),
                    0,
                    0,
                ]
            ),
            (3, [1, 0]),
            (10, [1, 2, 0, 0x0B])
        );
        var binary = type switch
        {
            "assert_malformed" => (byte[])[1],
            "assert_invalid" => invalid,
            _ => ScriptExecutionFixture.ImportFunctions("failed"),
        };
        var commands =
            ScriptExecutionFixture.Module(0, "$A")
            + ","
            + """{"type":"register","line":1,"name":"$missing","as":"failed"},"""
            + "{\"type\":\""
            + type
            + "\",\"line\":1,\"filename\":\"a.2.wasm\",\"module_type\":\"binary\",\"text\":\"\"},"
            + """{"type":"register","line":1,"name":"$A","as":"after"}""";

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Empty),
            (2, binary)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[2].Outcome).IsEqualTo(outcome);
            await Assert.That(results[2].LastStage).IsEqualTo((CaseStage?)stage);
            await Assert.That(state.LastModule).IsSameReferenceAs(state.ResolveModule("$A"));
            await Assert.That(state.LastModule!.Value).IsNotNull();
            await Assert.That(results[3].Outcome).IsEqualTo(CaseOutcome.Passed);
            if (outcome == CaseOutcome.Blocked)
            {
                await Assert
                    .That(results[2].Cause!.Direct)
                    .IsEquivalentTo([new CaseId("a.wast", 1)]);
                await Assert
                    .That(results[2].Cause!.Origins)
                    .IsEquivalentTo([new CaseId("a.wast", 1)]);
            }
            else
            {
                await Assert.That(results[2].Cause).IsNull();
            }
        }
    }

    [Test]
    public async Task 未登録名へのimport_Instantiateのリンク不成立を記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");

        // Act
        var results = ScriptExecutionFixture.Execute(
            ScriptExecutionFixture.Module(0),
            state,
            (0, ScriptExecutionFixture.ImportFunctions("missing"))
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Length).IsEqualTo(1);
            await Assert.That(results[0].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(results[0].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(results[0].Diagnostics[0].Reason).IsEqualTo("MissingImport");
        }
    }
}
