using System.Collections.Immutable;
using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

public class SuiteExecutor_ExecuteInputTests
{
    [Test]
    public async Task 実行済みactionから中断を通知_記録済みの結果と未処理の末尾を保持する()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var state = new ScriptState("a.wast");
        var host = new WasmHostModule("host");
        host.Define(
            "f",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    cancellation.Cancel();
                    return new([]);
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
        var document = ScriptDocument.Parse(
            Encoding.UTF8.GetBytes(
                """
                {"source_filename":"a.wast","commands":[
                 {"type":"module","line":1,"filename":"a.0.wasm"},
                 {"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[]},
                 {"type":"action","line":1,"action":{"type":"invoke","field":"f","args":[]},"expected":[]}]}
                """
            ),
            "a.json"
        );
        var input = new InputVerification(
            new("a.wast", new string('a', 64)),
            document,
            [],
            ImmutableDictionary<int, ImmutableArray<byte>>.Empty.Add(0, [.. binary]),
            []
        );

        // Act
        var (result, interruption) = SuiteExecutor.ExecuteInput(input, state, cancellation.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Status).IsEqualTo(InputRunStatus.Incomplete);
            await Assert.That(result.Cases.Count).IsEqualTo(2);
            await Assert.That(result.Cases.All(x => x.Outcome == CaseOutcome.Passed)).IsTrue();
            await Assert.That(result.CommandCount).IsEqualTo((int?)3);
            await Assert.That(result.UnprocessedCount).IsEqualTo(1);
            await Assert
                .That(interruption!.ExceptionType)
                .IsEqualTo(typeof(OperationCanceledException).FullName);
        }
    }
}
