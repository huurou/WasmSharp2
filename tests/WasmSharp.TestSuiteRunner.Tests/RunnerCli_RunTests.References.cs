using TUnit.Assertions.Enums;
using WasmSharp.Tests.Fixtures;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    [Test]
    [Arguments("zero", "i31ref", true, "00000000")]
    [Arguments("zero", "eqref", true, "00000000")]
    [Arguments("zero", "anyref", true, "00000000")]
    [Arguments("zero", "externref", true, "00000000")]
    [Arguments("zero", "refnull", false, "00000000")]
    [Arguments("negative", "i31ref", true, "7fffffff")]
    [Arguments("null", "refnull", true, null)]
    [Arguments("null", "i31ref", false, null)]
    [Arguments("null", "eqref", false, null)]
    public async Task 定数式で作ったi31参照またはnullを返す_ビット列と参照の種類に従って照合する(
        string export,
        string expected,
        bool matched,
        string? bits
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var module = ScriptExecutionFixture.Binary(
            // i31ref型のglobalをref.i31(0)、ref.i31(-1)、ref.null i31で初期化する。
            (6, [3, 0x6c, 0, 0x41, 0, 0xfb, 0x1c, 0x0b, 0x6c, 0, 0x41, 0x7f, 0xfb, 0x1c, 0x0b, 0x6c, 0, 0xd0, 0x6c, 0x0b]),
            (
                7,
                [
                    3,
                    .. ScriptExecutionFixture.Name("zero"),
                    3,
                    0,
                    .. ScriptExecutionFixture.Name("negative"),
                    3,
                    1,
                    .. ScriptExecutionFixture.Name("null"),
                    3,
                    2,
                ]
            )
        );
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"get","field":"{{export}}"},"expected":[{"type":"{{expected}}"}]}
            """,
            module
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(assertion.Outcome).IsEqualTo(matched ? CaseOutcome.Passed : CaseOutcome.Failed);
            var actual = assertion.ActualValues.Single();
            await Assert.That(actual.Type).IsEqualTo(bits is null ? "anyref" : "i31ref");
            await Assert.That(actual.IsNull).IsEqualTo(bits is null);
            await Assert.That(actual.Bits).IsEqualTo(bits);
        }
    }

    [Test]
    [Arguments("funcref", "function", true)]
    [Arguments("funcref", "null", false)]
    [Arguments("anyref", "function", false)]
    [Arguments("anyref", "null", true)]
    public async Task Core3の値を省略した参照条件で関数参照を比較する_関数実体の有無に応じた結果を保存する(
        string type,
        string export,
        bool matched
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var module = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 1, 0x70]),
            (3, [1, 0]),
            (6, [2, 0x70, 0, 0xd2, 0, 0x0b, 0x70, 0, 0xd0, 0x70, 0x0b]),
            (7, [2, .. ScriptExecutionFixture.Name("function"), 3, 0, .. ScriptExecutionFixture.Name("null"), 3, 1]),
            (10, [1, 4, 0, 0xd2, 0, 0x0b])
        );
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"get","field":"{{export}}"},"expected":[{"type":"{{type}}"}]}
            """,
            module
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(assertion.Outcome).IsEqualTo(matched ? CaseOutcome.Passed : CaseOutcome.Failed);
            await Assert.That(assertion.ExpectedValues.Single().Type).IsEqualTo(type);
            await Assert.That(assertion.ExpectedValues.Single().Value).IsNull();
            await Assert.That(assertion.ActualValues.Single().Type).IsEqualTo("funcref");
            await Assert.That(assertion.ActualValues.Single().IsNull).IsEqualTo(export == "null");
        }
    }

    [Test]
    [Arguments(WasmSpecVersion.Core3, "null", "refnull", null, true)]
    [Arguments(WasmSpecVersion.Core3, "7", "refnull", null, false)]
    [Arguments(WasmSpecVersion.Core3, "null", "nullfuncref", null, true)]
    [Arguments(WasmSpecVersion.Core3, "null", "funcref", "null", true)]
    [Arguments(WasmSpecVersion.Core2, "null", "funcref", "null", false)]
    [Arguments(WasmSpecVersion.Core3, "7", "arrayref", null, false)]
    [Arguments(WasmSpecVersion.Core3, "7", "funcref", null, false)]
    [Arguments(WasmSpecVersion.Core3, "null", "externref", null, true)]
    [Arguments(WasmSpecVersion.Core3, "7", "externref", null, true)]
    [Arguments(WasmSpecVersion.Core3, "7", "externref", "7", true)]
    [Arguments(WasmSpecVersion.Core3, "7", "externref", "8", false)]
    [Arguments(WasmSpecVersion.Core3, "null", "anyref", null, true)]
    [Arguments(WasmSpecVersion.Core3, "7", "anyref", null, true)]
    [Arguments(WasmSpecVersion.Core3, "7", "anyref", "null", false)]
    [Arguments(WasmSpecVersion.Core3, "7", "anyref", "7", false)]
    public async Task 番号付きまたはnullの外部参照を返す_指定された参照条件と比較して表記と結果を保存する(
        WasmSpecVersion version,
        string argument,
        string type,
        string? value,
        bool matched
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var expected = value is null ? $$"""{"type":"{{type}}"}""" : $$"""{"type":"{{type}}","value":"{{value}}"}""";
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"id","args":[{"type":"externref","value":"{{argument}}"}]},"expected":[{{expected}}]}
            """,
            ExternrefIdentity()
        );

        // Act
        var (_, cases) = RunAssertions(fixture, version);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(assertion.Outcome).IsEqualTo(matched ? CaseOutcome.Passed : CaseOutcome.Failed);
            await Assert.That(assertion.ExpectedValues.Single().Type).IsEqualTo(type);
            await Assert.That(assertion.ExpectedValues.Single().Value).IsEqualTo(value);
            var actual = assertion.ActualValues.Single();
            await Assert.That(actual.Type).IsEqualTo("externref");
            await Assert.That(actual.ExternrefNumber).IsEqualTo(argument == "null" ? null : (uint?)7);
        }
    }

    [Test]
    public async Task 番号付きのホスト参照と外部参照を相互に変換する_同じ番号でも参照の種類を区別する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var module = ScriptExecutionFixture.Binary(
            (1, [2, 0x60, 1, 0x6f, 1, 0x6e, 0x60, 1, 0x6e, 1, 0x6f]),
            (3, [2, 0, 1]),
            (
                7,
                [2, .. ScriptExecutionFixture.Name("internalize"), 0, 0, .. ScriptExecutionFixture.Name("externalize"), 0, 1]
            ),
            // 各関数は引数をany.convert_externまたはextern.convert_anyで変換する。
            (10, [2, 6, 0, 0x20, 0, 0xfb, 0x1a, 0x0b, 6, 0, 0x20, 0, 0xfb, 0x1b, 0x0b])
        );
        fixture.ReplaceScriptA(
            """
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"internalize","args":[{"type":"externref","value":"7"}]},"expected":[{"type":"anyref","value":"7"}]},
            {"type":"assert_return","line":3,"action":{"type":"invoke","field":"externalize","args":[{"type":"anyref","value":"7"}]},"expected":[{"type":"externref","value":"7"}]},
            {"type":"assert_return","line":4,"action":{"type":"invoke","field":"internalize","args":[{"type":"externref","value":"7"}]},"expected":[{"type":"externref","value":"7"}]},
            {"type":"assert_return","line":5,"action":{"type":"invoke","field":"externalize","args":[{"type":"anyref","value":"7"}]},"expected":[{"type":"anyref","value":"7"}]},
            {"type":"assert_return","line":6,"action":{"type":"invoke","field":"internalize","args":[{"type":"externref","value":"8"}]},"expected":[{"type":"anyref","value":"7"}]},
            {"type":"assert_return","line":7,"action":{"type":"invoke","field":"externalize","args":[{"type":"anyref","value":"null"}]},"expected":[{"type":"refnull"}]}
            """,
            module
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(cases.Select(x => x.Outcome))
                .IsEquivalentTo(
                    [
                        CaseOutcome.Passed,
                        CaseOutcome.Passed,
                        CaseOutcome.Passed,
                        CaseOutcome.Failed,
                        CaseOutcome.Failed,
                        CaseOutcome.Failed,
                        CaseOutcome.Passed,
                    ],
                    CollectionOrdering.Matching
                );
            var values = cases.Skip(1).Select(x => x.ActualValues.Single()).ToArray();
            await Assert
                .That(values.Select(x => (x.Type, x.ExternrefNumber)))
                .IsEquivalentTo(
                    new (string, uint?)[]
                    {
                        ("anyref", 7),
                        ("externref", 7),
                        ("anyref", 7),
                        ("externref", 7),
                        ("anyref", 8),
                        ("externref", null),
                    },
                    CollectionOrdering.Matching
                );
            await Assert.That(values[0].Token).IsEqualTo(values[2].Token);
            await Assert.That(values[0].Token).IsNotEqualTo(values[4].Token);
            await Assert.That(values[5].IsNull).IsTrue();
        }
    }

    [Test]
    [Arguments("anyref", CaseOutcome.Passed)]
    [Arguments("eqref", CaseOutcome.Failed)]
    public async Task ホスト参照を型判定して返す_anyとeqの期待値を区別する(string expected, CaseOutcome outcome)
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var module = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 1, 0x6e, 3, 0x6e, 0x7f, 0x7f]),
            (3, [1, 0]),
            (7, [1, .. ScriptExecutionFixture.Name("test"), 0, 0]),
            // 引数そのものと、ref.test (ref eq)、ref.test (ref any)の判定を返す。
            (10, [1, 14, 0, 0x20, 0, 0x20, 0, 0xfb, 0x14, 0x6d, 0x20, 0, 0xfb, 0x14, 0x6e, 0x0b])
        );
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"test","args":[{"type":"anyref","value":"0"}]},
             "expected":[{"type":"{{expected}}"},{"type":"i32","value":"0"},{"type":"i32","value":"1"}]}
            """,
            module
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(assertion.Outcome).IsEqualTo(outcome);
            await Assert.That(assertion.ActualValues[0].IsNull).IsFalse();
            await Assert.That(assertion.ActualValues[1].Bits).IsEqualTo("00000000");
            await Assert.That(assertion.ActualValues[2].Bits).IsEqualTo("00000001");
        }
    }

    [Test]
    public async Task 外部参照を要求する関数へホスト参照を渡す_呼び出し時の引数型不一致を記録する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.ReplaceScriptA(
            """
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"action","line":2,"action":{"type":"invoke","field":"id","args":[{"type":"anyref","value":"7"}]}},
            {"type":"assert_return","line":3,"action":{"type":"invoke","field":"id","args":[{"type":"externref","value":"7"}]},
             "expected":[{"type":"externref","value":"7"}]}
            """,
            ExternrefIdentity()
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var rejected = cases[1];
        var diagnostic = rejected.Diagnostics.Single();
        using (Assert.Multiple())
        {
            await Assert.That(rejected.Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(rejected.LastStage).IsEqualTo(CaseStage.Invoke);
            await Assert.That(rejected.ActualValues).IsEmpty();
            await Assert.That(diagnostic.Operation).IsEqualTo("invoke");
            await Assert.That(diagnostic.ExceptionType).IsEqualTo(typeof(ArgumentException).FullName);
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.Passed);
        }
    }

    [Test]
    [Arguments("2", true)]
    [Arguments("null", false)]
    public async Task 番号またはnullのanyrefを渡す対象のmoduleが成立しない_参照期待値を保存して失敗したmoduleを原因に記録する(
        string argument,
        bool unsupported
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"f","args":[{"type":"anyref","value":"{{argument}}"}]},"expected":[
                {"type":"arrayref"},{"type":"eqref"},{"type":"i31ref"},{"type":"structref"},
                {"type":"nullref"},{"type":"nullexternref"},{"type":"nullexnref"},{"type":"exnref","value":"null"},
                {"type":"anyref","value":"2"},{"type":"anyref","value":"null"}]}
            """,
            unsupported ? Core3OfficialBinary.Tag : [0]
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(unsupported ? CaseOutcome.RuntimeUnsupported : CaseOutcome.Failed);
            await Assert.That(assertion.Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(assertion.Cause!.Origins).IsEquivalentTo([new CaseId(CorpusFixture.A, 0)]);
            await Assert
                .That(assertion.ExpectedValues.Select(x => (x.Type, x.Value)).ToArray())
                .IsEquivalentTo(
                    [
                        ("arrayref", null),
                        ("eqref", null),
                        ("i31ref", null),
                        ("structref", null),
                        ("nullref", null),
                        ("nullexternref", null),
                        ("nullexnref", null),
                        ("exnref", "null"),
                        ("anyref", "2"),
                        ("anyref", "null"),
                    ],
                    CollectionOrdering.Matching
                );
        }
    }

    [Test]
    [Arguments("""[{"type":"anyref","value":"-1"}]""", "[]", "create_arguments", "action.args[0].value")]
    [Arguments(
        "[]",
        """[{"type":"either","values":[{"type":"i32","value":"1"},{"type":"refnull","value":"7"}]}]""",
        "parse_expected",
        "expected[0].values[1].value"
    )]
    public async Task Core3の対象moduleが成立せず参照表記も不正である_不正な項目の位置をランナー異常として保存する(
        string arguments,
        string expected,
        string operation,
        string path
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"f","args":{{arguments}}},"expected":{{expected}}}
            """,
            Core3OfficialBinary.Tag
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.RuntimeUnsupported);
            await Assert.That(assertion.Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(assertion.Cause).IsNull();
            var diagnostic = assertion.Diagnostics.Single();
            await Assert.That(diagnostic.Operation).IsEqualTo(operation);
            await Assert.That(diagnostic.Message).Contains(path);
        }
    }

    [Test]
    [Arguments("refnull", true)]
    [Arguments("funcref", false)]
    public async Task 異なる参照条件を同じeitherに指定する_外部参照のnullに一致する候補と表記を保存する(
        string secondType,
        bool matched
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var module = ScriptExecutionFixture.Binary(
            (6, [1, 0x6f, 0, 0xd0, 0x6f, 0x0b]),
            (7, [1, .. ScriptExecutionFixture.Name("g"), 3, 0])
        );
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"get","field":"g"},"expected":[
                {"type":"either","values":[{"type":"anyref","value":"7"},{"type":"{{secondType}}"}]}]}
            """,
            module
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(assertion.Outcome).IsEqualTo(matched ? CaseOutcome.Passed : CaseOutcome.Failed);
            await Assert.That(assertion.ExpectedValues.Single().Type).IsEqualTo("either");
            await Assert
                .That(assertion.ExpectedValues.Single().Values!.Select(x => (x.Type, x.Value)))
                .IsEquivalentTo([("anyref", "7"), (secondType, null)], CollectionOrdering.Matching);
        }
    }

    private static byte[] ExternrefIdentity()
    {
        return ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 1, 0x6f, 1, 0x6f]),
            (3, [1, 0]),
            (7, [1, .. ScriptExecutionFixture.Name("id"), 0, 0]),
            (10, [1, 4, 0, 0x20, 0, 0x0b])
        );
    }
}
