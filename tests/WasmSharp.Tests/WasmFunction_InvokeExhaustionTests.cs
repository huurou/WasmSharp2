using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Hostから再帰的に再入する_深さ上限で中断し独立して再実行できる(
        bool withInstance
    )
    {
        // Arrange
        WasmFunction function = null!;
        var reenter = true;
        WasmResults Callback(ReadOnlySpan<WasmValue> values)
        {
            return reenter ? function.Invoke([]) : new([]);
        }

        var host = withInstance
            ? WasmFunction.CreateHost(new([], []), (_, values) => Callback(values))
            : WasmFunction.CreateHost(new([], []), Callback);
        function = CreateHostCallingInstance(host, 4).GetFunction("run");
        var cleared = false;

        // Act & Assert
        var exhaustion = await Assert
            .That(() =>
            {
                try
                {
                    function.Invoke([]);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        reenter = false;
        var result = function.Invoke([]);
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
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("recursive", 0, 0), ("empty", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B]), ([], [0x0B]))
                )
            )
            .Validate()
            .Instantiate([], new(10_000));
        var cleared = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                try
                {
                    instance.GetFunction("recursive").Invoke([]);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        var result = instance.GetFunction("empty").Invoke([]);
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
    public async Task Hostがランタイムと同型の例外を投げる_位置と段階を含め元実体を伝播し次の実行を妨げない(
        bool exhaustion
    )
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Instantiate, 12345, 42);
        Exception expected = exhaustion
            ? new WasmExhaustionException(
                "ホスト由来",
                WasmExhaustionReason.HostStackLimit,
                null,
                location
            )
            : new WasmTrapException("ホスト由来", WasmTrapReason.Unreachable, location);
        var fail = true;
        var host = WasmFunction.CreateHost(
            new([], []),
            (WasmHostCallback)(_ => fail ? throw expected : new([]))
        );
        var function = CreateHostCallingInstance(host, 2).GetFunction("run");
        var cleared = false;

        // Act & Assert
        var actual = await Assert
            .That(() =>
            {
                try
                {
                    function.Invoke([]);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .Throws<Exception>();
        fail = false;
        var result = function.Invoke([]);
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsSameReferenceAs(expected);
            await Assert.That(cleared).IsTrue();
            await Assert.That(result.Values).IsEmpty();
        }
    }
}
