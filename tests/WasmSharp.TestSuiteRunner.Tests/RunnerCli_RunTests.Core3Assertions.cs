using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests;

internal partial class RunnerCli_RunTests
{
    [Test]
    public async Task Core3のassert_exceptionの関数が正常終了する_元の公式ケースに不一致と戻り値を保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.ReplaceScriptA(
            """
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_exception","line":17,"action":{"type":"invoke","field":"f","args":[]}}
            """,
            ScriptExecutionFixture.Exports()
        );

        // Act
        var (exitCode, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var result = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(result.Id).IsEqualTo(new CaseId("a.wast", 1));
            await Assert.That(result.Line).IsEqualTo(17);
            await Assert.That(result.CommandType).IsEqualTo("assert_exception");
            await Assert.That(result.Category).IsEqualTo(CaseCategory.Assertion);
            await Assert.That(result.ExpectedText).IsNull();
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(result.LastStage).IsEqualTo(CaseStage.Invoke);
            await Assert.That(result.ActualValues.Select(x => x.Bits!)).IsEquivalentTo(["00000007"]);
            await Assert.That(result.Diagnostics).IsEmpty();
        }
    }

    [Test]
    [Arguments("trap", CaseOutcome.Failed, typeof(WasmTrapException))]
    [Arguments("recurse", CaseOutcome.Failed, typeof(WasmExhaustionException))]
    [Arguments("absent", CaseOutcome.RunnerError, typeof(ArgumentException))]
    public async Task Core3のassert_exceptionの関数で例外を観測する_例外の種類に応じた分類と診断を保存する(
        string field,
        CaseOutcome outcome,
        Type exception
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.ReplaceScriptA(
            $$$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_exception","line":2,"action":{"type":"invoke","field":"{{{field}}}","args":[]}}
            """,
            FailureActions
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var result = cases[1];
        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(outcome);
            await Assert.That(result.LastStage).IsEqualTo(CaseStage.Invoke);
            await Assert.That(result.Diagnostics.Single().ExceptionType).IsEqualTo(exception.FullName);
        }
    }

    [Test]
    public async Task Core3のassert_exceptionの対象instanceを生成できない_直接の原因と未対応の元の公式ケースを保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        fixture.ReplaceScriptA(
            """
            {"type":"module_definition","line":1,"name":"D","filename":"a.0.wasm","module_type":"binary"},
            {"type":"module_instance","line":2,"instance":"I","module":"D"},
            {"type":"assert_exception","line":3,"action":{"type":"invoke","module":"I","field":"f","args":[]}}
            """,
            Core3OfficialBinary.Tag
        );

        // Act
        var (_, cases) = RunAssertions(fixture, WasmSpecVersion.Core3);

        // Assert
        var result = cases[2];
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.RuntimeUnsupported);
            await Assert.That(result.Id).IsEqualTo(new CaseId("a.wast", 2));
            await Assert.That(result.Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(result.LastStage).IsNull();
            await Assert.That(result.Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 1)]);
            await Assert.That(result.Cause.Origins).IsEquivalentTo([new CaseId("a.wast", 0)]);
        }
    }

    [Test]
    [Arguments(WasmSpecVersion.Core2, CaseOutcome.Failed, CaseStage.Decode, "integer too large")]
    [Arguments(WasmSpecVersion.Core3, CaseOutcome.Passed, CaseStage.Instantiate, "incompatible import type")]
    public async Task 否定assertionを採用版の診断で照合する_段階と分類と診断の先頭が一致したケースだけ成立する(
        WasmSpecVersion version,
        CaseOutcome importOutcome,
        CaseStage importStage,
        string importDiagnostic
    )
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        var resultTypes = version == WasmSpecVersion.Core2 ? ""","expected":[]""" : "";
        fixture.ReplaceScriptA(
            $$$"""
            {"type":"module","line":1,"filename":"a.0.wasm"},
            {"type":"assert_trap","line":2,"action":{"type":"invoke","field":"trap","args":[]},"text":"unreachable"{{{resultTypes}}}},
            {"type":"assert_trap","line":3,"action":{"type":"invoke","field":"trap","args":[]},"text":"integer overflow"{{{resultTypes}}}},
            {"type":"assert_exhaustion","line":4,"action":{"type":"invoke","field":"trap","args":[]},"text":"unreachable"{{{resultTypes}}}},
            {"type":"assert_invalid","line":5,"filename":"a.1.wasm","module_type":"binary","text":"magic header not detected"},
            {"type":"assert_unlinkable","line":6,"filename":"a.2.wasm","module_type":"binary","text":"incompatible import type"}
            """,
            FailureActions,
            [0xff, 0x61, 0x73, 0x6d, 1, 0, 0, 0],
            // 32ビットのspectest.memoryを、最小0ページの64ビットメモリとして要求する。
            ScriptExecutionFixture.Binary(
                (2, [1, .. ScriptExecutionFixture.Name("spectest"), .. ScriptExecutionFixture.Name("memory"), 2, 4, 0])
            )
        );

        // Act
        var (_, cases) = RunAssertions(fixture, version);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cases[1].Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(cases[1].ExpectedText).IsEqualTo("unreachable");
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[2].ExpectedText).IsEqualTo("integer overflow");
            await Assert.That(cases[2].Diagnostics.Single().Message).IsEqualTo("unreachable executed");
            await Assert.That(cases[3].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[3].Diagnostics.Single().ExceptionType).IsEqualTo(typeof(WasmTrapException).FullName);
            await Assert.That(cases[4].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[4].LastStage).IsEqualTo(CaseStage.Decode);
            await Assert.That(cases[4].Diagnostics.Single().Message).IsEqualTo("magic header not detected");
            await Assert.That(cases[5].Outcome).IsEqualTo(importOutcome);
            await Assert.That(cases[5].LastStage).IsEqualTo(importStage);
            await Assert.That(cases[5].Diagnostics.Single().Message).StartsWith(importDiagnostic);
        }
    }

    // trapはunreachableを実行し、recurseは呼び出し深さの上限に達するまで自身を呼ぶ。
    private static byte[] FailureActions =>
        ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (3, [2, 0, 0]),
            (7, [2, .. ScriptExecutionFixture.Name("trap"), 0, 0, .. ScriptExecutionFixture.Name("recurse"), 0, 1]),
            (10, [2, 3, 0, 0x00, 0x0B, 4, 0, 0x10, 1, 0x0B])
        );

    private (int ExitCode, List<CaseResult> Cases) RunAssertions(CorpusFixture fixture, WasmSpecVersion version)
    {
        var core2 = fixture.Manifest.Profile.ToProfile();
        var core3 = _profileLoader.LoadCore3() with { Inputs = core2.Inputs };
        var profile = version == WasmSpecVersion.Core2 ? core2 : core3;
        var manifest = Complete(fixture.Manifest with { Profile = ProfileSnapshot.FromProfile(profile) });
        _reportStore.Save(manifest, fixture.ManifestPath);
        var output = Path.Combine(fixture.OutputRoot, "run.json");
        var cli = new RunnerCli(core2, () => core3);
        var exitCode = cli.Run(
            ["run", "--manifest", fixture.ManifestPath, "--output", output],
            TextWriter.Null,
            TextWriter.Null
        );
        return (exitCode, _reportStore.ReadRunReport(output).Report.Inputs[0].Cases);
    }
}
