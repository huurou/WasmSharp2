using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

internal class InterpreterContext_TryEnterCallTests
{
    [Test]
    public async Task 上限1で2段目へ入る_深さを変えずに拒否し退出後は再入場できる()
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var context = executionContext.Enter(new(1), out var isOutermost);
        bool first;
        bool second;
        bool retry;
        int rejectedDepth;
        int exitedDepth;

        // Act
        try
        {
            first = context.TryEnterCall();
            second = context.TryEnterCall();
            rejectedDepth = context.CallDepth;
            context.ExitCall();
            exitedDepth = context.CallDepth;
            retry = context.TryEnterCall();
            context.ExitCall();
        }
        finally
        {
            executionContext.Exit(isOutermost);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsTrue();
            await Assert.That(second).IsFalse();
            await Assert.That(rejectedDepth).IsEqualTo(1);
            await Assert.That(exitedDepth).IsEqualTo(0);
            await Assert.That(retry).IsTrue();
        }
    }
}
