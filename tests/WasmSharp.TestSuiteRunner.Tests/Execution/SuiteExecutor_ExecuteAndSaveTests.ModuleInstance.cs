using System.Text;
using WasmSharp.Tests.Fixtures;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public partial class SuiteExecutor_ExecuteAndSaveTests
{
    [Test]
    public async Task 公式の最大32bitテーブルの定義をCore3で実行_インスタンス化せず検証までで成立を保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        ReplaceScript(
            fixture,
            """{"type":"module_definition","line":9,"filename":"a.0.wasm","module_type":"binary"}""",
            Core3OfficialBinary.MaximumTable32
        );
        var output = Path.Combine(fixture.OutputRoot, "definition.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var stored = _reportStore.ReadRunReport(output);
        var actual = stored.Report.Inputs[0].Cases[0];
        using (Assert.Multiple())
        {
            await Assert.That(stored.Report.Inputs[0].Issues).IsEmpty();
            await Assert.That(actual.CommandType).IsEqualTo("module_definition");
            await Assert.That(actual.Category).IsEqualTo(CaseCategory.Setup);
            await Assert.That(actual.Outcome).IsEqualTo(CaseOutcome.Passed);
            await Assert.That(actual.LastStage).IsEqualTo(CaseStage.Validate);
            await Assert.That(actual.Diagnostics).IsEmpty();
        }
    }

    [Test]
    public async Task 同じ定義から2つのinstanceを生成する_片方の状態変更が波及せずcommandごとの結果を保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        ReplaceScript(
            fixture,
            """
            {"type":"module_definition","line":1,"name":"D","filename":"a.0.wasm","module_type":"binary"},
            {"type":"module_instance","line":2,"instance":"I1","module":"D"},
            {"type":"module_instance","line":3,"instance":"I2","module":"D"},
            {"type":"action","line":4,"action":{"type":"invoke","module":"I1","field":"inc","args":[]},"expected":[]},
            {"type":"assert_return","line":5,"action":{"type":"get","module":"I1","field":"g"},"expected":[{"type":"i32","value":"2"}]},
            {"type":"assert_return","line":6,"action":{"type":"get","module":"I2","field":"g"},"expected":[{"type":"i32","value":"1"}]},
            {"type":"module_instance","line":7},
            {"type":"assert_return","line":8,"action":{"type":"get","field":"g"},"expected":[{"type":"i32","value":"1"}]}
            """,
            StartCounter
        );
        var output = Path.Combine(fixture.OutputRoot, "instances.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases.Select(x => x.Outcome)).IsEquivalentTo(Enumerable.Repeat(CaseOutcome.Passed, 8));
            await Assert.That(cases[0].LastStage).IsEqualTo(CaseStage.Validate);
            await Assert.That(cases[1].CommandType).IsEqualTo("module_instance");
            await Assert.That(cases[1].Category).IsEqualTo(CaseCategory.Setup);
            await Assert.That(cases[1].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(cases[2].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(cases[4].ActualValues.Select(x => x.Bits)).IsEquivalentTo(["00000002"]);
            await Assert.That(cases[5].ActualValues.Select(x => x.Bits)).IsEquivalentTo(["00000001"]);
            await Assert.That(cases[6].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(cases[7].ActualValues.Select(x => x.Bits)).IsEquivalentTo(["00000001"]);
        }
    }

    [Test]
    public async Task 未実装の定義を参照するinstanceと登録_blockedとして元の定義のruntime_unsupportedまでたどれる()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        ReplaceScript(
            fixture,
            """
            {"type":"module_definition","line":3,"name":"M","filename":"a.0.wasm","module_type":"binary"},
            {"type":"module_instance","line":10,"instance":"I1","module":"M"},
            {"type":"module_instance","line":11,"instance":"I2","module":"M"},
            {"type":"register","line":12,"name":"I1","as":"I1"}
            """,
            Core3OfficialBinary.TagAfterTableInitializer
        );
        var output = Path.Combine(fixture.OutputRoot, "blocked.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.RuntimeUnsupported);
            await Assert.That(cases[0].Diagnostics[0].Feature).IsEqualTo("tag section");
            foreach (var instance in cases.Skip(1).Take(2))
            {
                await Assert.That(instance.Outcome).IsEqualTo(CaseOutcome.Blocked);
                await Assert.That(instance.LastStage).IsNull();
                await Assert.That(instance.Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 0)]);
                await Assert.That(instance.Cause.Origins).IsEquivalentTo([new CaseId("a.wast", 0)]);
            }
            await Assert.That(cases[3].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(cases[3].Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 1)]);
            await Assert.That(cases[3].Cause!.Origins).IsEquivalentTo([new CaseId("a.wast", 0)]);
        }
    }

    [Test]
    public async Task startでtrapした通常moduleの定義からinstanceを生成する_定義を不成立にせずインスタンス化の失敗として保存する()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        ReplaceScript(
            fixture,
            """
            {"type":"module","line":1,"name":"M","filename":"a.0.wasm"},
            {"type":"module_instance","line":2,"instance":"I","module":"M"},
            {"type":"register","line":3,"name":"I","as":"I"}
            """,
            StartTrap
        );
        var output = Path.Combine(fixture.OutputRoot, "start-trap.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases[0].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[0].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(cases[1].Outcome).IsEqualTo(CaseOutcome.Failed);
            await Assert.That(cases[1].LastStage).IsEqualTo(CaseStage.Instantiate);
            await Assert.That(cases[1].Diagnostics[0].ExceptionType).IsEqualTo(typeof(Exceptions.WasmTrapException).FullName);
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(cases[2].Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 1)]);
            await Assert.That(cases[2].Cause!.Origins).IsEquivalentTo([new CaseId("a.wast", 1)]);
        }
    }

    [Test]
    public async Task 読み取れないmodule_instance_以前のinstanceへ戻さず後続actionを原因付きでblockedにする()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        ReplaceScript(
            fixture,
            """
            {"type":"module_definition","line":1,"name":"D","filename":"a.0.wasm","module_type":"binary"},
            {"type":"module_instance","line":2,"instance":"I","module":"D"},
            {"type":"module_instance","line":3,"instance":"I","module":7},
            {"type":"assert_return","line":4,"action":{"type":"get","module":"I","field":"g"},"expected":[{"type":"i32","value":"1"}]},
            {"type":"assert_return","line":5,"action":{"type":"get","field":"g"},"expected":[{"type":"i32","value":"1"}]}
            """,
            StartCounter
        );
        var output = Path.Combine(fixture.OutputRoot, "invalid-instance.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(cases[2].Category).IsEqualTo(CaseCategory.Setup);
            foreach (var action in cases.Skip(3))
            {
                await Assert.That(action.Outcome).IsEqualTo(CaseOutcome.Blocked);
                await Assert.That(action.Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 2)]);
            }
        }
    }

    [Test]
    public async Task 読み取れないmodule_definition_以前の定義へ戻さず直近の定義を使うinstanceを原因付きでblockedにする()
    {
        // Arrange
        using var fixture = CorpusFixture.Create();
        ReplaceScript(
            fixture,
            """
            {"type":"module_definition","line":1,"filename":"a.0.wasm","module_type":"binary"},
            {"type":"module_definition","line":2,"filename":"a.0.wasm","module_type":7},
            {"type":"module_instance","line":3}
            """,
            StartCounter
        );
        var output = Path.Combine(fixture.OutputRoot, "invalid-definition.json");

        // Act
        SuiteExecutor.ExecuteAndSave(
            SuiteExecutor_ExecuteTests.Complete(fixture.Manifest),
            fixture.ManifestPath,
            WasmSpecVersion.Core3,
            output,
            _reportStore
        );

        // Assert
        var cases = _reportStore.ReadRunReport(output).Report.Inputs[0].Cases;
        using (Assert.Multiple())
        {
            await Assert.That(cases[1].Outcome).IsEqualTo(CaseOutcome.RunnerError);
            await Assert.That(cases[2].Outcome).IsEqualTo(CaseOutcome.Blocked);
            await Assert.That(cases[2].Cause!.Direct).IsEquivalentTo([new CaseId("a.wast", 1)]);
        }
    }

    // startでunreachableを実行する
    private static byte[] StartTrap { get; } =
        ScriptExecutionFixture.Binary((1, [1, 0x60, 0, 0]), (3, [1, 0]), (8, [0]), (10, [1, 3, 0, 0x00, 0x0B]));

    // startとincが同じ可変globalを1ずつ増やし、gとしてexportする
    private static byte[] StartCounter { get; } =
        ScriptExecutionFixture.Binary(
            (1, [1, 0x60, 0, 0]),
            (3, [2, 0, 0]),
            (6, [1, 0x7F, 1, 0x41, 0, 0x0B]),
            (7, [2, .. ScriptExecutionFixture.Name("inc"), 0, 1, .. ScriptExecutionFixture.Name("g"), 3, 0]),
            (8, [0]),
            (10, [2, .. IncrementBody, .. IncrementBody])
        );

    private static byte[] IncrementBody => [9, 0, 0x23, 0, 0x41, 1, 0x6A, 0x24, 0, 0x0B];

    // 入力Aの素材を、指定したcommandとa.{index}.wasmのbinaryだけへ置き換える。
    private static void ReplaceScript(CorpusFixture fixture, string commands, params byte[][] modules)
    {
        fixture.UpdateInput(CorpusFixture.A, x => x with { Artifacts = [] });
        fixture.WriteArtifact(
            CorpusFixture.A,
            "modules/a.json",
            Encoding.UTF8.GetBytes("{\"source_filename\":\"a.wast\",\"commands\":[" + commands + "]}")
        );
        for (var i = 0; i < modules.Length; i++)
        {
            fixture.WriteArtifact(CorpusFixture.A, $"modules/a.{i}.wasm", modules[i]);
        }
    }
}
