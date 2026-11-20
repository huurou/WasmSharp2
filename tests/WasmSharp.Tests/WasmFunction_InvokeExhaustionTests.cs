using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Hostから再帰的に再入する_深さ上限で中断し独立して再実行できる(
        bool withInstance,
        bool differentInstance
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        WasmFunction function = null!;
        var reenter = true;
        WasmResults Callback(WasmExecutionContext callbackContext, ReadOnlySpan<WasmValue> values)
        {
            return reenter ? function.Invoke(callbackContext, []) : new([]);
        }

        var host = withInstance
            ? WasmFunction.CreateHost(new([], []), (callbackContext, _, values) => Callback(callbackContext, values))
            : WasmFunction.CreateHost(new([], []), Callback);
        function = CreateHostCallingInstance(executionContext, host, 4).GetFunction("run");
        var entry = function;
        if (differentInstance)
        {
            function = CreateHostCallingInstance(executionContext, host, 20).GetFunction("run");
        }
        var cleared = false;

        // Act & Assert
        var exhaustion = await Assert
            .That(() =>
            {
                try
                {
                    entry.Invoke(executionContext, []);
                }
                finally
                {
                    cleared = executionContext.Current is null;
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        reenter = false;
        var result = function.Invoke(executionContext, []);
        using (Assert.Multiple())
        {
            await Assert.That(exhaustion!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exhaustion.Limit).IsEqualTo(4);
            await Assert.That(exhaustion.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(exhaustion.Location.FunctionIndex).IsEqualTo(1U);
            await Assert.That(cleared).IsTrue();
            await Assert.That(result.Values).IsEmpty();
        }
    }

    [Test]
    public async Task 定義関数が直接再帰する_設定上限で中断した後に独立した関数を実行できる()
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("recursive", 0, 0), ("empty", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B]), ([], [0x0B]))
                ),
                WasmSpecVersion.Core2
            )
            .Validate()
            .Instantiate(executionContext, [], new(10_000));
        var cleared = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                try
                {
                    instance.GetFunction("recursive").Invoke(executionContext, []);
                }
                finally
                {
                    cleared = executionContext.Current is null;
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        var result = instance.GetFunction("empty").Invoke(executionContext, []);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exception.Limit).IsEqualTo(10_000);
            await Assert.That(cleared).IsTrue();
            await Assert.That(result.Values).IsEmpty();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Hostがランタイムと同型の例外を投げる_位置と段階を含め元実体を伝播し次の実行を妨げない(bool exhaustion)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var location = new WasmFailureLocation(WasmProcessingStage.Instantiate, 12345, 42);
        Exception expected = exhaustion
            ? new WasmExhaustionException("ホスト由来", WasmExhaustionReason.HostStackLimit, null, location)
            : new WasmTrapException("ホスト由来", WasmTrapReason.Unreachable, location);
        var fail = true;
        var host = WasmFunction.CreateHost(
            new([], []),
            (WasmHostCallback)((callbackContext, _) => fail ? throw expected : new([]))
        );
        var function = CreateHostCallingInstance(executionContext, host, 2).GetFunction("run");
        var cleared = false;

        // Act & Assert
        var actual = await Assert
            .That(() =>
            {
                try
                {
                    function.Invoke(executionContext, []);
                }
                finally
                {
                    cleared = executionContext.Current is null;
                }
            })
            .Throws<Exception>();
        fail = false;
        var result = function.Invoke(executionContext, []);
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsSameReferenceAs(expected);
            await Assert.That(cleared).IsTrue();
            await Assert.That(result.Values).IsEmpty();
        }
    }
}
