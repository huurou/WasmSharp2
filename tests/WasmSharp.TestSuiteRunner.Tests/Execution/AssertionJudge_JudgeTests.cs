using System.Collections.Immutable;
using System.Text.Json;
using WasmSharp.Exceptions;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class AssertionJudge_JudgeTests
{
    [Test]
    [Arguments("assert_malformed", CaseStage.Decode, "decode")]
    [Arguments("assert_invalid", CaseStage.Validate, "validate")]
    [Arguments("assert_unlinkable", CaseStage.Instantiate, "missing")]
    [Arguments("assert_unlinkable", CaseStage.Instantiate, "kind")]
    [Arguments("assert_unlinkable", CaseStage.Instantiate, "type")]
    [Arguments("assert_uninstantiable", CaseStage.Instantiate, "trap")]
    [Arguments("assert_trap", CaseStage.Invoke, "trap")]
    [Arguments("assert_trap", CaseStage.Get, "trap")]
    [Arguments("assert_exhaustion", CaseStage.Invoke, "exhaustion")]
    [Arguments("assert_exhaustion", CaseStage.Get, "exhaustion")]
    public async Task 否定assertionの段階と失敗が一致する_Passedと診断を返す(
        string type,
        CaseStage stage,
        string failure
    )
    {
        // Arrange
        var command = Negative(type, stage);
        var exception = Failure(failure);
        var observation = Observe(stage) with { Exception = exception };

        // Act
        var result = AssertionJudge.Judge(command, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(result.ExpectedText).IsEqualTo("expected");
            await Assert.That(result.LastStage).IsEqualTo(stage);
            await Assert.That(result.Diagnostics[0].Message).IsEqualTo(exception.Message);
            await Assert
                .That(result.Diagnostics[0].ExceptionType)
                .IsEqualTo(exception.GetType().FullName);
            await Assert.That(result.Diagnostics[0].Operation).IsEqualTo("public_call");
        }
    }

    [Test]
    [Arguments("assert_malformed", CaseStage.Validate, "decode")]
    [Arguments("assert_invalid", CaseStage.Decode, "validate")]
    [Arguments("assert_unlinkable", CaseStage.Invoke, "missing")]
    [Arguments("assert_unlinkable", CaseStage.Instantiate, "unknown_link")]
    [Arguments("assert_uninstantiable", CaseStage.Invoke, "trap")]
    [Arguments("assert_uninstantiable", CaseStage.Instantiate, "missing")]
    [Arguments("assert_trap", CaseStage.Instantiate, "trap")]
    [Arguments("assert_trap", CaseStage.Get, "trap", CaseStage.Invoke)]
    [Arguments("assert_trap", CaseStage.Invoke, "exhaustion")]
    [Arguments("assert_exhaustion", CaseStage.Instantiate, "exhaustion")]
    [Arguments("assert_exhaustion", CaseStage.Invoke, "trap")]
    [Arguments("assert_invalid", CaseStage.Decode, "decode")]
    public async Task 否定assertionの段階または失敗が異なる_Failedと観測を返す(
        string type,
        CaseStage stage,
        string failure,
        CaseStage? actionStage = null
    )
    {
        // Arrange
        var command = Negative(type, actionStage ?? stage);
        var exception = Failure(failure);

        // Act
        var result = AssertionJudge.Judge(command, Observe(stage) with { Exception = exception });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(result.LastStage).IsEqualTo(stage);
            await Assert.That(result.Diagnostics[0].Message).IsEqualTo(exception.Message);
        }
    }

    [Test]
    [Arguments("assert_malformed", CaseStage.Decode)]
    [Arguments("assert_invalid", CaseStage.Validate)]
    [Arguments("assert_unlinkable", CaseStage.Instantiate)]
    [Arguments("assert_uninstantiable", CaseStage.Instantiate)]
    [Arguments("assert_trap", CaseStage.Invoke)]
    [Arguments("assert_exhaustion", CaseStage.Invoke)]
    public async Task 期待した失敗が発生しない_Failedを返す(string type, CaseStage stage)
    {
        // Arrange
        var command = Negative(type, stage);

        // Act
        var result = AssertionJudge.Judge(command, Observe(stage));

        // Assert
        await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Failed);
    }

    [Test]
    [Arguments("unsupported", CaseOutcome.RuntimeUnsupported)]
    [Arguments("limit", CaseOutcome.RunnerError)]
    [Arguments("oom", CaseOutcome.RunnerError)]
    [Arguments("invoke", CaseOutcome.RunnerError)]
    [Arguments("platform", CaseOutcome.RunnerError)]
    [Arguments("argument", CaseOutcome.RunnerError)]
    [Arguments("unknown", CaseOutcome.RunnerError)]
    public async Task 判定できない例外_期待失敗へ置き換えず原因を保存する(
        string failure,
        CaseOutcome outcome
    )
    {
        // Arrange
        var command = Negative("assert_exhaustion", CaseStage.Invoke);
        var exception = Failure(failure);

        // Act
        var result = AssertionJudge.Judge(
            command,
            Observe(CaseStage.Invoke) with
            {
                Exception = exception,
            }
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(outcome);
            await Assert
                .That(result.Diagnostics[0].ExceptionType)
                .IsEqualTo(exception.GetType().FullName);
            await Assert.That(result.Diagnostics[0].Message).IsEqualTo("expected");
        }
    }

    [Test]
    [Arguments("decode")]
    [Arguments("validate")]
    [Arguments("missing")]
    [Arguments("trap")]
    [Arguments("exhaustion")]
    [Arguments("unsupported")]
    public async Task CallbackがWasm例外を投げる_型にかかわらずRunnerErrorを返す(string failure)
    {
        // Arrange
        var exception = Failure(failure);
        var observation = Observe(CaseStage.Invoke) with
        {
            Exception = exception,
            CallbackException = exception,
        };

        // Act
        var result = AssertionJudge.Judge(Negative("assert_trap", CaseStage.Invoke), observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(result.Diagnostics[0].FromCallback).IsTrue();
        }
    }

    [Test]
    public async Task 同型の別例外がcallback記録にある_Callback由来とは扱わない()
    {
        // Arrange
        var observation = Observe(CaseStage.Invoke) with
        {
            Exception = Failure("trap"),
            CallbackException = Failure("trap"),
        };

        // Act
        var result = AssertionJudge.Judge(Negative("assert_trap", CaseStage.Invoke), observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(result.Diagnostics[0].FromCallback).IsFalse();
        }
    }

    [Test]
    [Arguments("module", CaseStage.Instantiate)]
    [Arguments("register", CaseStage.Register)]
    [Arguments("action", CaseStage.Invoke)]
    [Arguments("action", CaseStage.Get)]
    public async Task セットアップと単独actionが正常完了する_Passedと結果を返す(
        string type,
        CaseStage stage
    )
    {
        // Arrange
        ScriptCommand command = type switch
        {
            "module" => new ModuleCommand(2, 3, null, "a.wasm", ScriptModuleType.Binary),
            "register" => new RegisterCommand(2, 3, null, "a"),
            _ => new ActionCommand(2, 3, Action(stage), [WasmValueKind.F64]),
        };
        var observation = Observe(stage) with { Values = [new("i32") { Bits = "00000001" }] };

        // Act
        var result = AssertionJudge.Judge(command, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(result.Id).IsEqualTo(new CaseId("a.wast", 2));
            await Assert.That(result.CommandType).IsEqualTo(type);
            await Assert.That(result.ActualValues[0].Bits).IsEqualTo("00000001");
        }
    }

    [Test]
    [Arguments(false, CaseOutcome.Passed)]
    [Arguments(true, CaseOutcome.Failed)]
    public async Task Assert_returnの値比較_相違に応じて分類し全実値を保存する(
        bool differs,
        CaseOutcome outcome
    )
    {
        // Arrange
        var expected = new ExpectedValue(WasmValueKind.I32, "1", null, []);
        var command = new AssertReturnCommand(2, 3, Action(CaseStage.Invoke), [expected]);
        var actual = new WasmResults([WasmValue.FromI32(differs ? 2 : 1)]);
        var state = new ScriptState("a.wast");
        var observation = Observe(CaseStage.Invoke) with
        {
            Values = [.. actual.Values.Select(x => ValueCodec.Record(x, state))],
            Mismatches = ValueMatcher.Match(ValueMatcher.Parse(command.Expected), actual, state),
        };

        // Act
        var result = AssertionJudge.Judge(command, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(outcome);
            await Assert.That(result.ExpectedValues[0].Value).IsEqualTo("1");
            await Assert
                .That(result.ActualValues[0].Bits)
                .IsEqualTo(differs ? "00000002" : "00000001");
            await Assert.That(result.Diagnostics.Count).IsEqualTo(differs ? 1 : 0);
        }
    }

    [Test]
    [Arguments(WasmImportInspectionReason.UnsupportedFeature, CaseOutcome.RuntimeUnsupported)]
    [Arguments(WasmImportInspectionReason.ImplementationLimit, CaseOutcome.RunnerError)]
    [Arguments(WasmImportInspectionReason.MalformedBinary, CaseOutcome.RunnerError)]
    [Arguments(WasmImportInspectionReason.UnresolvedType, CaseOutcome.RunnerError)]
    public async Task Import情報取得に失敗する_Malformedの成立へ置き換えない(
        WasmImportInspectionReason reason,
        CaseOutcome outcome
    )
    {
        // Arrange
        var exception = new WasmImportInspectionException(
            "expected",
            reason,
            "simd",
            new(WasmProcessingStage.Decode, 7),
            [],
            null
        );

        // Act
        var result = AssertionJudge.Judge(
            Negative("assert_malformed", CaseStage.Decode),
            Observe(CaseStage.InspectImports) with
            {
                Exception = exception,
            }
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(outcome);
            await Assert.That(result.Diagnostics[0].Reason).IsEqualTo(reason.ToString());
        }
    }

    [Test]
    public async Task 未実装の公開診断_機能と位置と未確認範囲を保存する()
    {
        // Arrange
        var exception = new WasmUnsupportedFeatureException(
            "expected",
            "simd",
            new(WasmProcessingStage.Validate, 7, 2, 10),
            [new(WasmProcessingStage.Validate, 7, 20, "remaining")]
        );

        // Act
        var result = AssertionJudge.Judge(
            Negative("assert_invalid", CaseStage.Validate),
            Observe(CaseStage.Validate) with
            {
                Exception = exception,
            }
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.RuntimeUnsupported);
            await Assert.That(result.Diagnostics[0].Feature).IsEqualTo("simd");
            await Assert
                .That(result.Diagnostics[0].Location)
                .IsEqualTo(new FailureLocationRecord("Validate", 7, 2, 10));
            await Assert
                .That(result.Diagnostics[0].UnverifiedRanges[0])
                .IsEqualTo(new UnverifiedRangeRecord("Validate", 7, 20, "remaining"));
        }
    }

    [Test]
    public async Task 既知失敗に依存する_Blockedと直接原因と元原因を保存する()
    {
        // Arrange
        var cause = new UnavailableCause(new("a.wast", 1), [new("a.wast", 0)]);
        var observation = Observe(CaseStage.Validate) with { BlockedBy = [cause] };

        // Act
        var result = AssertionJudge.Judge(
            Negative("assert_unlinkable", CaseStage.Instantiate),
            observation
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(result.Cause!.Direct.SequenceEqual([cause.Command])).IsTrue();
            await Assert.That(result.Cause.Origins.SequenceEqual(cause.Origins)).IsTrue();
        }
    }

    [Test]
    [Arguments("module")]
    [Arguments("assert_malformed")]
    public async Task Text形式は実行対象外_OutOfScopeを返す(string type)
    {
        // Arrange
        ScriptCommand command =
            type == "module"
                ? new ModuleCommand(2, 3, null, "missing.wat", ScriptModuleType.Text)
                : new AssertMalformedCommand(
                    2,
                    3,
                    "missing.wat",
                    "expected",
                    ScriptModuleType.Text
                );

        // Act
        var result = AssertionJudge.Judge(command, Observe(null));

        // Assert
        await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.OutOfScope);
    }

    [Test]
    public async Task 不正commandや素材異常_RunnerErrorと元診断を保存する()
    {
        // Arrange
        var diagnostic = new CaseDiagnostic("read_json", "invalid") { SourceJson = "{bad}" };
        var command = new InvalidCommand(2, 3, "unknown", diagnostic);

        // Act
        var invalid = AssertionJudge.Judge(command, Observe(null));
        var material = AssertionJudge.Judge(
            new ModuleCommand(2, 3, null, "a.wat", ScriptModuleType.Text),
            Observe(null) with
            {
                Diagnostics = [diagnostic],
            }
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(invalid.Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(invalid.Diagnostics[0].SourceJson).IsEqualTo("{bad}");
            await Assert.That(material.Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(material.Diagnostics[0].Operation).IsEqualTo("read_json");
        }
    }

    [Test]
    [Arguments("module", CaseStage.Decode, "decode")]
    [Arguments("module", CaseStage.Validate, "validate")]
    [Arguments("module", CaseStage.Instantiate, "missing")]
    [Arguments("module", CaseStage.Instantiate, "trap")]
    [Arguments("action", CaseStage.Invoke, "trap")]
    [Arguments("action", CaseStage.Invoke, "exhaustion")]
    [Arguments("return", CaseStage.Invoke, "trap")]
    public async Task 正常完了を期待した処理でWasm失敗を観測する_Failedを返す(
        string type,
        CaseStage stage,
        string failure
    )
    {
        // Arrange
        ScriptCommand command = type switch
        {
            "module" => new ModuleCommand(2, 3, null, "a.wasm", ScriptModuleType.Binary),
            "action" => new ActionCommand(2, 3, Action(stage), []),
            _ => new AssertReturnCommand(2, 3, Action(stage), []),
        };

        // Act
        var result = AssertionJudge.Judge(
            command,
            Observe(stage) with
            {
                Exception = Failure(failure),
            }
        );

        // Assert
        await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Failed);
    }

    [Test]
    public async Task 判定結果の可変リストを変更する_観測と次回の判定結果を変更しない()
    {
        // Arrange
        var value = new ValueRecord("i32") { Bits = "00000001" };
        var print = new PrintRecord("print_i32") { Arguments = [value] };
        var diagnostic = new CaseDiagnostic("verify", "bad hash")
        {
            UnverifiedRanges = [new("Decode", 1, 2, "range")],
        };
        var command = new AssertReturnCommand(
            2,
            3,
            Action(CaseStage.Invoke),
            [new(WasmValueKind.V128, null, LaneType.I64, ["1", "2"])]
        );
        var observation = Observe(CaseStage.Invoke) with
        {
            Values = [value],
            Prints = [print],
            Diagnostics = [diagnostic],
        };

        // Act
        var first = AssertionJudge.Judge(command, observation);
        first.ActualValues.Clear();
        first.ExpectedValues[0].Lanes.Clear();
        first.Prints[0].Arguments.Clear();
        first.Diagnostics[0].UnverifiedRanges.Clear();
        var second = AssertionJudge.Judge(command, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.ActualValues).Count().IsEqualTo(1);
            await Assert.That(second.ExpectedValues[0].Lanes.SequenceEqual(["1", "2"])).IsTrue();
            await Assert.That(second.Prints[0].Arguments).Count().IsEqualTo(1);
            await Assert.That(second.Diagnostics[0].UnverifiedRanges).Count().IsEqualTo(1);
            await Assert.That(observation.Exception).IsNull();
            await Assert.That(observation.LastStage).IsEqualTo(CaseStage.Invoke);
        }
    }

    [Test]
    [Arguments("assert_malformed", CaseStage.Decode, "decode")]
    [Arguments("assert_invalid", CaseStage.Validate, "validate")]
    [Arguments("assert_unlinkable", CaseStage.Instantiate, "missing")]
    [Arguments("assert_uninstantiable", CaseStage.Instantiate, "trap")]
    [Arguments("assert_trap", CaseStage.Invoke, "trap")]
    [Arguments("assert_exhaustion", CaseStage.Invoke, "exhaustion")]
    public async Task 全否定assertionで期待診断と前方一致しない_Failedを保存して読み戻せる(
        string type,
        CaseStage stage,
        string failure
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var command = Negative(type, stage);
        var exception = Failure(failure, "different diagnostic");
        var observation = Observe(stage) with { Exception = exception };
        var path = directory.Combine("run.json");

        // Act
        var result = AssertionJudge.Judge(command, observation);
        ReportStore.Save(
            RunReportFixture.Create(
                RunReportFixture.Case("a.wast", 0, CaseCategory.Setup, CaseOutcome.Passed),
                RunReportFixture.Case("a.wast", 1, CaseCategory.Setup, CaseOutcome.Passed),
                result
            ),
            path
        );
        var stored = ReportStore.ReadRunReport(path);
        var saved = stored.Report.Inputs[0].Cases[2];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(stored.Issues).IsEmpty();
            await Assert.That(saved.Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(saved.ExpectedText).IsEqualTo("expected");
            await Assert.That(saved.Diagnostics[0].Message).IsEqualTo("different diagnostic");
            await Assert
                .That(saved.Diagnostics[0].ExceptionType)
                .IsEqualTo(exception.GetType().FullName);
            await Assert.That(saved.LastStage).IsEqualTo(stage);
            await Assert.That(saved.Diagnostics[0].Stage).IsEqualTo(stage);
            await Assert
                .That(JsonSerializer.Serialize(saved))
                .IsEqualTo(JsonSerializer.Serialize(result));
        }
    }

    [Test]
    [Arguments("assert_malformed", CaseStage.Decode, "decode")]
    [Arguments("assert_invalid", CaseStage.Validate, "validate")]
    [Arguments("assert_unlinkable", CaseStage.Instantiate, "missing")]
    [Arguments("assert_uninstantiable", CaseStage.Instantiate, "trap")]
    [Arguments("assert_trap", CaseStage.Invoke, "trap")]
    [Arguments("assert_exhaustion", CaseStage.Invoke, "exhaustion")]
    public async Task 全否定assertionで期待診断の後ろに補助説明がある_Passedと加工前の診断を返す(
        string type,
        CaseStage stage,
        string failure
    )
    {
        // Arrange
        var command = Negative(type, stage);
        var observation = Observe(stage) with
        {
            Exception = Failure(failure, "expected: offset=42"),
        };

        // Act
        var result = AssertionJudge.Judge(command, observation);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(result.ExpectedText).IsEqualTo("expected");
            await Assert.That(result.Diagnostics[0].Message).IsEqualTo("expected: offset=42");
        }
    }

    [Test]
    [Arguments("expected", "Expected", CaseOutcome.Failed)]
    [Arguments("expected", " expected", CaseOutcome.Failed)]
    [Arguments(" expected", "expected", CaseOutcome.Failed)]
    [Arguments("expected ", "expected", CaseOutcome.Failed)]
    [Arguments("out of bounds", "out  of bounds", CaseOutcome.Failed)]
    [Arguments("limit 10", "limit 11", CaseOutcome.Failed)]
    [Arguments("expected", "prefix expected", CaseOutcome.Failed)]
    [Arguments("é", "e\u0301", CaseOutcome.Failed)]
    [Arguments("expected", "expected\n詳細", CaseOutcome.Passed)]
    [Arguments("", "anything", CaseOutcome.Passed)]
    public async Task 診断の文字列をそのまま照合する_Ordinal前方一致だけで判定する(
        string expected,
        string actual,
        CaseOutcome outcome
    )
    {
        // Arrange
        var command = Negative("assert_trap", CaseStage.Invoke, expected);
        var exception = Failure("trap", actual);

        // Act
        var result = AssertionJudge.Judge(
            command,
            Observe(CaseStage.Invoke) with
            {
                Exception = exception,
            }
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(outcome);
            await Assert.That(result.ExpectedText).IsEqualTo(expected);
            await Assert.That(result.Diagnostics[0].Message).IsEqualTo(actual);
            await Assert.That(result.Diagnostics[0].Reason).IsEqualTo("Unreachable");
            await Assert
                .That(result.Diagnostics[0].Location)
                .IsEqualTo(new FailureLocationRecord("Invoke", 7, null, null));
        }
    }

    private static CommandObservation Observe(CaseStage? stage)
    {
        return new(new("a.wast", 2), "public_call", stage);
    }

    private static ScriptAction Action(CaseStage stage)
    {
        return stage == CaseStage.Get
            ? new GetAction(null, "value")
            : new InvokeAction(null, "run", []);
    }

    private static ScriptCommand Negative(string type, CaseStage stage, string text = "expected")
    {
        return type switch
        {
            "assert_malformed" => new AssertMalformedCommand(
                2,
                3,
                "a.wasm",
                text,
                ScriptModuleType.Binary
            ),
            "assert_invalid" => new AssertInvalidCommand(
                2,
                3,
                "a.wasm",
                text,
                ScriptModuleType.Binary
            ),
            "assert_unlinkable" => new AssertUnlinkableCommand(
                2,
                3,
                "a.wasm",
                text,
                ScriptModuleType.Binary
            ),
            "assert_uninstantiable" => new AssertUninstantiableCommand(
                2,
                3,
                "a.wasm",
                text,
                ScriptModuleType.Binary
            ),
            "assert_trap" => new AssertTrapCommand(2, 3, Action(stage), text, []),
            _ => new AssertExhaustionCommand(2, 3, Action(stage), text, []),
        };
    }

    private static Exception Failure(string kind, string message = "expected")
    {
        return kind switch
        {
            "decode" => new WasmDecodeException(message),
            "validate" => new WasmValidateException(message),
            "missing" or "kind" or "type" => new WasmInstantiateException(
                message,
                kind == "missing" ? WasmInstantiateReason.MissingImport
                    : kind == "kind" ? WasmInstantiateReason.KindMismatch
                    : WasmInstantiateReason.TypeMismatch,
                0,
                "a",
                "b",
                WasmExternalKind.Function,
                new(WasmProcessingStage.Instantiate, 7)
            ),
            "unknown_link" => new WasmInstantiateException(message),
            "trap" => new WasmTrapException(
                message,
                WasmTrapReason.Unreachable,
                new(WasmProcessingStage.Invoke, 7)
            ),
            "exhaustion" => new WasmExhaustionException(
                message,
                WasmExhaustionReason.CallDepthLimit,
                1024,
                new(WasmProcessingStage.Invoke, 7)
            ),
            "unsupported" => new WasmUnsupportedFeatureException(message),
            "limit" => new WasmImplementationLimitException(
                message,
                WasmImplementationLimitReason.CollectionSize,
                10
            ),
            "oom" => new OutOfMemoryException(message),
            "invoke" => new WasmInvokeException(message),
            "platform" => new WasmPlatformCapabilityException(message),
            "argument" => new ArgumentException(message),
            _ => new InvalidOperationException(message),
        };
    }
}
