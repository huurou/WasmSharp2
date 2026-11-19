using TUnit.Assertions.Enums;
using WasmSharp.Tests.Fixtures;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public partial class SuiteExecutor_ExecuteAndSaveTests
{
    [Test]
    [Arguments("function", true)]
    [Arguments("null", false)]
    public async Task Core3の値を省略したfuncrefを比較する_関数実体の有無で判定して保存する(string export, bool matched)
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
            {"type":"assert_return","line":2,"action":{"type":"get","field":"{{export}}"},"expected":[{"type":"funcref"}]}
            """,
            module
        );

        // Act
        var cases = RunAndReadReferenceCases(fixture);

        // Assert
        var assertion = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(assertion.Outcome).IsEqualTo(matched ? CaseOutcome.Passed : CaseOutcome.Failed);
            await Assert.That(assertion.ExpectedValues.Single().Type).IsEqualTo("funcref");
            await Assert.That(assertion.ExpectedValues.Single().Value).IsNull();
            await Assert.That(assertion.ActualValues.Single().IsNull).IsEqualTo(!matched);
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
        var identity = ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 1, 0x6f, 1, 0x6f]),
            (3, [1, 0]),
            (7, [1, .. ScriptExecutionFixture.Name("id"), 0, 0]),
            (10, [1, 4, 0, 0x20, 0, 0x0b])
        );
        var expected = value is null ? $$"""{"type":"{{type}}"}""" : $$"""{"type":"{{type}}","value":"{{value}}"}""";
        fixture.ReplaceScriptA(
            $$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_return","line":2,"action":{"type":"invoke","field":"id","args":[{"type":"externref","value":"{{argument}}"}]},"expected":[{{expected}}]}
            """,
            identity
        );

        // Act
        var cases = RunAndReadReferenceCases(fixture, version);

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
        var cases = RunAndReadReferenceCases(fixture);

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
        var cases = RunAndReadReferenceCases(fixture);

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

    private List<CaseResult> RunAndReadReferenceCases(CorpusFixture fixture, WasmSpecVersion version = WasmSpecVersion.Core3)
    {
        var output = Path.Combine(fixture.OutputRoot, "references.json");
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            version,
            output,
            _reportStore
        );
        return _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
    }
}
