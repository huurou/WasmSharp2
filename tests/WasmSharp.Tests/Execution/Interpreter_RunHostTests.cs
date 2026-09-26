using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_RunHostTests
{
    [Test]
    public async Task CLRスタックの余裕がない_hostを実行せず未計測上限の結果を返し深さを復元する()
    {
        // Arrange
        var called = false;
        var host = WasmFunction.CreateHost(
            new([], []),
            _ =>
            {
                called = true;
                return new([]);
            }
        );

        // Act
        var observed = ExecutionStackFixture.RunAtLimit(() =>
        {
            var context = InterpreterContext.Enter(new(10), out var outermost);
            try
            {
                var result = Interpreter.RunHost(context, host, null, []);
                return (
                    Result: result,
                    context.CallDepth,
                    ContextPreserved: ReferenceEquals(context, InterpreterContext.Current)
                );
            }
            finally
            {
                InterpreterContext.Exit(outermost);
            }
        });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(observed.Result.Status).IsEqualTo(ExecutionStatus.Exhaustion);
            await Assert
                .That(observed.Result.ExhaustionReason)
                .IsEqualTo(WasmExhaustionReason.HostStackLimit);
            await Assert.That(observed.Result.Limit).IsNull();
            await Assert.That(observed.CallDepth).IsEqualTo(0);
            await Assert.That(observed.ContextPreserved).IsTrue();
            await Assert.That(called).IsFalse();
        }
    }
}
