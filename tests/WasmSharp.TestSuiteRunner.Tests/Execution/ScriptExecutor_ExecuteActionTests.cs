using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public partial class ScriptExecutor_ExecuteTests
{
    [Test]
    public async Task 直近moduleの失敗と名前指定_既知の失敗だけを原因付きblockedとし独立actionを続行する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            ScriptExecutionFixture.Module(0, "$A")
            + ","
            + ScriptExecutionFixture.Module(1)
            + ","
            + """
                {"type":"action","line":1,"action":{"type":"invoke","module":"$A","field":"f","args":[]},"expected":[]},
                {"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[]},
                {"type":"action","line":1,"action":{"type":"invoke","module":"$missing","field":"f","args":[]},"expected":[]},
                {"type":"action","line":1,"action":{"type":"invoke","module":"$A","field":"missing","args":[]},"expected":[]},
                {"type":"action","line":1,"action":{"type":"get","module":"$A","field":"g"},"expected":[]}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Exports()),
            (1, [1])
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[2].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(results[3].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(results[3].Cause!.Origins).IsEquivalentTo([new CaseId("a.wast", 1)]);
            await Assert.That(results[4].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(results[5].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(results[5].Diagnostics[0].Operation).IsEqualTo("invoke");
            await Assert.That(results[6].Outcome).IsEqualTo(CaseOutcome.Passed);
        }
    }

    [Test]
    public async Task CallbackがWasm例外を投げる_期待trapに置き換えず例外実体の由来を記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var exception = new WasmSharp.Exceptions.WasmTrapException("callback trap");
        var host = new WasmHostModule("host");
        host.Define(
            "f",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    state.CallbackException = exception;
                    throw exception;
                }
            )
        );
        state.Register(host);
        var binary = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (
                2,
                [
                    1,
                    .. ScriptExecutionFixture.Name("host"),
                    .. ScriptExecutionFixture.Name("f"),
                    0,
                    0,
                ]
            ),
            (7, [1, .. ScriptExecutionFixture.Name("f"), 0, 0])
        );
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + """
                {"type":"assert_trap","line":1,"action":{"type":"invoke","field":"f","args":[]},"text":"callback trap","expected":[]}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (0, binary));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(results[1].Diagnostics[0].FromCallback).IsTrue();
            await Assert.That(results[1].Diagnostics[0].Message).IsEqualTo(exception.Message);
            await Assert
                .That(results[1].Diagnostics[0].ExceptionType)
                .IsEqualTo(exception.GetType().FullName);
        }
    }

    [Test]
    public async Task 全値型のinvokeと複数結果_ビット列と参照を順序どおり記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        const string VALUES = """
            {"type":"i32","value":"4294967295"},
            {"type":"i64","value":"9223372036854775808"},
            {"type":"f32","value":"2147483648"},
            {"type":"f64","value":"9221120237041090561"},
            {"type":"v128","lane_type":"i32","value":["1","2","3","4"]},
            {"type":"funcref","value":"null"},
            {"type":"externref","value":"123"}
            """;
        byte[] kinds = [0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F];
        var echo = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 7, .. kinds, 7, .. kinds]),
            (3, [1, 0]),
            (7, [1, .. ScriptExecutionFixture.Name("f"), 0, 0]),
            (
                10,
                [
                    1,
                    16,
                    0,
                    .. Enumerable.Range(0, 7).SelectMany(x => new byte[] { 0x20, (byte)x }),
                    0x0B,
                ]
            )
        );
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + "{\"type\":\"assert_return\",\"line\":1,\"action\":{\"type\":\"invoke\",\"field\":\"f\",\"args\":["
            + VALUES
            + "]},\"expected\":["
            + VALUES
            + "]}";

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (0, echo));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(results[1].ActualValues.Count).IsEqualTo(7);
            await Assert.That(results[1].ActualValues[0].Bits).IsEqualTo("ffffffff");
            await Assert.That(results[1].ActualValues[1].Bits).IsEqualTo("8000000000000000");
            await Assert.That(results[1].ActualValues[2].Bits).IsEqualTo("80000000");
            await Assert.That(results[1].ActualValues[3].Bits).IsEqualTo("7ff8000000000001");
            await Assert.That(results[1].ActualValues[4].Low64).IsEqualTo("0000000200000001");
            await Assert.That(results[1].ActualValues[5].IsNull).IsTrue();
            await Assert.That(results[1].ActualValues[6].ExternrefNumber).IsEqualTo((uint?)123);
        }
    }

    [Test]
    public async Task 単独actionの型宣言とassert_returnの値不一致_正常終了と不一致を区別する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + """
                {"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[{"type":"i64"}]},
                {"type":"assert_return","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[{"type":"i32","value":"8"}]},
                {"type":"assert_return","line":1,"action":{"type":"get","field":"g"},"expected":[{"type":"i32","value":"0"}]}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (0, ScriptExecutionFixture.Exports())
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(results[1].ActualValues[0].Bits).IsEqualTo("00000007");
            await Assert.That(results[2].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(results[2].Diagnostics[0].Operation).IsEqualTo("match_values");
            await Assert.That(results[3].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(results[3].LastStage).IsEqualTo(CaseStage.Get);
        }
    }

    [Test]
    public async Task 不正な引数と期待値_公開呼び出し前に拒否して副作用を起こさない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var count = 0;
        var host = new WasmHostModule("host");
        host.Define(
            "f",
            WasmFunction.CreateHost(
                new([WasmValueKind.I32], [WasmValueKind.I32]),
                x =>
                {
                    count++;
                    return new([x[0]]);
                }
            )
        );
        state.Register(host);
        var binary = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 1, 0x7F, 1, 0x7F]),
            (
                2,
                [
                    1,
                    .. ScriptExecutionFixture.Name("host"),
                    .. ScriptExecutionFixture.Name("f"),
                    0,
                    0,
                ]
            ),
            (7, [1, .. ScriptExecutionFixture.Name("f"), 0, 0])
        );
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + """
                {"type":"assert_return","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"i32","value":"1"}]},"expected":[{"type":"i32","value":"bad"}]},
                {"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[{"type":"i32","value":"bad"}]},"expected":[]}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (0, binary));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(count).IsEqualTo(0);
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(results[1].Diagnostics[0].Message).Contains("expected[0].value");
            await Assert.That(results[2].Diagnostics[0].Message).Contains("action.args[0].value");
            await Assert.That(results[2].LastStage).IsNull();
        }
    }

    [Test]
    public async Task Trap前のglobal更新_不一致後も現在値と診断を記録する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var binary = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (3, [1, 0]),
            (6, [1, 0x7F, 1, 0x41, 0, 0x0B]),
            (
                7,
                [
                    2,
                    .. ScriptExecutionFixture.Name("f"),
                    0,
                    0,
                    .. ScriptExecutionFixture.Name("g"),
                    3,
                    0,
                ]
            ),
            (10, [1, 7, 0, 0x41, 9, 0x24, 0, 0x00, 0x0B])
        );
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + """
                {"type":"assert_trap","line":1,"action":{"type":"invoke","field":"f","args":[]},"text":"unreachable","expected":[]},
                {"type":"assert_return","line":1,"action":{"type":"get","field":"g"},"expected":[{"type":"i32","value":"9"}]},
                {"type":"assert_trap","line":1,"action":{"type":"invoke","field":"f","args":[]},"text":"Wasmの実行中にtrap","expected":[]}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (0, binary));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(results[1].ExpectedText).IsEqualTo("unreachable");
            await Assert.That(results[1].Diagnostics[0].Reason).IsEqualTo("Unreachable");
            await Assert.That(results[1].Diagnostics[0].Location!.Stage).IsEqualTo("Invoke");
            await Assert.That(results[2].ActualValues[0].Bits).IsEqualTo("00000009");
            await Assert.That(results[3].Outcome).IsEqualTo(CaseOutcome.Passed);
        }
    }

    [Test]
    public async Task 再帰が固定上限に到達_Exhaustionと空結果を記録して継続する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var binary = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (3, [2, 0, 0]),
            (
                7,
                [
                    2,
                    .. ScriptExecutionFixture.Name("recursive"),
                    0,
                    0,
                    .. ScriptExecutionFixture.Name("empty"),
                    0,
                    1,
                ]
            ),
            (10, [2, 4, 0, 0x10, 0, 0x0B, 2, 0, 0x0B])
        );
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + """
                {"type":"assert_exhaustion","line":1,"action":{"type":"invoke","field":"recursive","args":[]},"text":"Wasmの実行資源","expected":[]},
                {"type":"assert_return","line":1,"action":{"type":"invoke","field":"empty","args":[]},"expected":[]}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (0, binary));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.All(x => x.Outcome == CaseOutcome.Passed)).IsTrue();
            await Assert.That(results[1].Diagnostics[0].Limit).IsEqualTo((int?)1024);
            await Assert.That(results[2].ActualValues.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Startとinvokeのprint_対応commandに記録して次のcommandへ残さない()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        state.Register(SpectestFactory.Create(state));
        var binary = ScriptExecutionFixture.Binary(
            (1, [2, 0x60, 1, 0x7F, 0, 0x60, 0, 0]),
            (
                2,
                [
                    1,
                    .. ScriptExecutionFixture.Name("spectest"),
                    .. ScriptExecutionFixture.Name("print_i32"),
                    0,
                    0,
                ]
            ),
            (3, [1, 1]),
            (7, [1, .. ScriptExecutionFixture.Name("f"), 0, 1]),
            (8, [1]),
            (10, [1, 6, 0, 0x41, 7, 0x10, 0, 0x0B])
        );
        var commands =
            ScriptExecutionFixture.Module(0)
            + ","
            + """
                {"type":"assert_return","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[]},
                {"type":"register","line":1,"as":"shared"}
                """;

        // Act
        var results = ScriptExecutionFixture.Execute(commands, state, (0, binary));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.All(x => x.Outcome == CaseOutcome.Passed)).IsTrue();
            await Assert.That(results[0].Prints[0].Arguments[0].Bits).IsEqualTo("00000007");
            await Assert.That(results[1].Prints.Count).IsEqualTo(1);
            await Assert.That(results[2].Prints.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Textのmoduleと未知command_対象外と構造異常を分け独立moduleを継続する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var commands =
            """
                {"type":"module","line":1,"filename":"missing.wat","module_type":"text"},
                {"type":"unknown","line":1},
                """ + ScriptExecutionFixture.Module(2);

        // Act
        var results = ScriptExecutionFixture.Execute(
            commands,
            state,
            (2, ScriptExecutionFixture.Empty)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results[0].Outcome).IsEqualTo(CaseOutcome.OutOfScope);
            await Assert.That(results[1].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(results[1].Category).IsNull();
            await Assert.That(results[2].Outcome).IsEqualTo(CaseOutcome.Passed);
        }
    }
}
