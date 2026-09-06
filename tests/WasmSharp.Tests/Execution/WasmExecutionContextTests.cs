using WasmSharp.Execution;

namespace WasmSharp.Tests.Execution;

public class WasmExecutionContext_TryEnterCallTests
{
    [Test]
    public async Task 上限1で2段目へ入る_深さを変えずに拒否し退出後は再入場できる()
    {
        // Arrange
        var context = WasmExecutionContext.Enter(new(1), out var isOutermost);
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
            context.Exit(isOutermost);
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

public class WasmExecutionContext_EnterTests
{
    [Test]
    public async Task 上限100の深さ50から上限10へ入る_同じコンテキストで51段目へ入る()
    {
        // Arrange
        var outer = WasmExecutionContext.Enter(new(100), out var isOutermost);
        WasmExecutionContext inner;
        bool isInnerOutermost;
        bool entered;
        int depth;
        int limit;
        bool outerRemains;

        // Act
        try
        {
            for (var i = 0; i < 50; i++)
            {
                outer.TryEnterCall();
            }
            inner = WasmExecutionContext.Enter(new(10), out isInnerOutermost);
            try
            {
                entered = inner.TryEnterCall();
                depth = inner.CallDepth;
                limit = inner.MaxCallDepth;
                inner.ExitCall();
            }
            finally
            {
                inner.Exit(isInnerOutermost);
            }
            outerRemains =
                ReferenceEquals(WasmExecutionContext.Current, outer) && outer.CallDepth == 50;
        }
        finally
        {
            while (outer.CallDepth > 0)
            {
                outer.ExitCall();
            }
            outer.Exit(isOutermost);
        }
        var cleared = WasmExecutionContext.Current is null;
        var next = WasmExecutionContext.Enter(new(10), out var isNextOutermost);
        var nextDepth = next.CallDepth;
        next.Exit(isNextOutermost);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(isOutermost).IsTrue();
            await Assert.That(isInnerOutermost).IsFalse();
            await Assert.That(ReferenceEquals(inner, outer)).IsTrue();
            await Assert.That(entered).IsTrue();
            await Assert.That(depth).IsEqualTo(51);
            await Assert.That(limit).IsEqualTo(100);
            await Assert.That(outerRemains).IsTrue();
            await Assert.That(cleared).IsTrue();
            await Assert.That(isNextOutermost).IsTrue();
            await Assert.That(ReferenceEquals(next, outer)).IsFalse();
            await Assert.That(next.MaxCallDepth).IsEqualTo(10);
            await Assert.That(nextDepth).IsEqualTo(0);
        }
    }
}

public class WasmExecutionContext_ExitTests
{
    [Test]
    public async Task 同期区間で例外が発生する_finallyで深さと現在の参照を戻し元の例外を保つ()
    {
        // Arrange
        var expected = new InvalidOperationException("内部入退出の検証");
        WasmExecutionContext? outer = null;
        var innerDepth = -1;
        var outerRemains = false;
        var cleared = false;
        var exitedDepth = -1;

        // Act & Assert
        var actual = await Assert
            .That(() =>
            {
                outer = WasmExecutionContext.Enter(new(100), out var isOutermost);
                try
                {
                    outer.TryEnterCall();
                    var inner = WasmExecutionContext.Enter(new(10), out var isInnerOutermost);
                    try
                    {
                        inner.TryEnterCall();
                        try
                        {
                            throw expected;
                        }
                        finally
                        {
                            inner.ExitCall();
                            inner.Exit(isInnerOutermost);
                        }
                    }
                    finally
                    {
                        innerDepth = outer.CallDepth;
                        outerRemains = ReferenceEquals(WasmExecutionContext.Current, outer);
                    }
                }
                finally
                {
                    outer.ExitCall();
                    outer.Exit(isOutermost);
                    cleared = WasmExecutionContext.Current is null;
                    exitedDepth = outer.CallDepth;
                }
            })
            .ThrowsExactly<InvalidOperationException>();

        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(actual, expected)).IsTrue();
            await Assert.That(innerDepth).IsEqualTo(1);
            await Assert.That(outerRemains).IsTrue();
            await Assert.That(exitedDepth).IsEqualTo(0);
            await Assert.That(cleared).IsTrue();
        }
    }
}
