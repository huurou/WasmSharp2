using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptExecutor_ObserveTests
{
    [Test]
    public async Task 異なる操作でcommandの処理が終わる_診断に使う最後の操作名を保持する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var input = new InputVerification(
            new SourceInput("a.wast", new string('a', 64)),
            null,
            [],
            ImmutableDictionary<int, ImmutableArray<byte>>.Empty
                .Add(0, [.. ScriptExecutionFixture.Exports()])
                .Add(4, [.. ScriptExecutionFixture.ImportFunctions("unavailable")]),
            ImmutableDictionary<int, CorpusDiagnostic>.Empty.Add(3, new CorpusDiagnostic("verify", "素材がありません。"))
        );
        var executor = new ScriptExecutor(input, WasmSpecVersion.Core2);
        executor.Execute(new ModuleCommand(0, 1, "$A", "a.0.wasm", ScriptModuleType.Binary), state);
        executor.Execute(new RegisterCommand(1, 2, "$missing", "unavailable"), state);
        var get = new GetAction("$A", "g");
        ScriptCommand[] commands =
        [
            new InvalidCommand(2, 3, "unknown", new CaseDiagnostic("read_command", "不明なcommandです。")),
            new ModuleCommand(3, 4, null, "a.3.wasm", ScriptModuleType.Binary),
            new ModuleCommand(4, 5, null, "a.4.wasm", ScriptModuleType.Binary),
            new RegisterCommand(5, 6, "$A", "registered"),
            new ActionCommand(6, 7, get, [WasmValueKind.I32]),
            new AssertReturnCommand(7, 8, get, [new ExpectedValue(WasmValueKind.I32, "0", null, [])]),
        ];
        string[] expectedOperations = ["read_command", "verify_module", "inspect_imports", "register", "record_values", "match_values"];

        // Act
        var observations = commands.Select(x => executor.Observe(x, state)).ToArray();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(observations.Select(x => x.Operation).SequenceEqual(expectedOperations)).IsTrue();
            await Assert.That(observations.All(x => x.Exception is null)).IsTrue();
        }
    }
}
