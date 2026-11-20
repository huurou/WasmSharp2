using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task 単独hostから別instanceを直接またはネストして呼ぶ_最初のWasm入口の上限を使う(
        bool withInstance,
        bool nested
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var b = CreateTwoCallInstance(executionContext, 2).GetFunction("run");
        var provider = new WasmHostModule("b");
        provider.Define("run", b);
        var a = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("b", "run", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B]))
                ),
                WasmSpecVersion.Core2
            )
            .Validate()
            .Instantiate(executionContext, [provider], new(3))
            .GetFunction("run");
        WasmResults Callback(WasmExecutionContext callbackContext, ReadOnlySpan<WasmValue> arguments)
        {
            return (nested ? a : b).Invoke(callbackContext, []);
        }

        var type = new WasmFunctionType([], [WasmValueKind.I32]);
        var host = withInstance
            ? WasmFunction.CreateHost(type, (callbackContext, _, arguments) => Callback(callbackContext, arguments))
            : WasmFunction.CreateHost(type, Callback);
        var access = CreateHostAccessInstance(executionContext);

        // Act
        var result = withInstance ? host.Invoke(executionContext, access, []) : host.Invoke(executionContext, []);

        // Assert
        await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 定義関数へnullまたは別instanceを指定する_指定を無視し定義元の上限で実行する(bool explicitNull)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var source = CreateTwoCallInstance(executionContext, 2);
        var other = CreateHostAccessInstance(executionContext);

        // Act
        var result = source.GetFunction("run").Invoke(executionContext, explicitNull ? null! : other, []);
        var cleared = executionContext.Current is null;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(cleared).IsTrue();
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Hostへ個数または型が異なる引数を渡す_両Invoke形式で実行前に拒否する(bool withInstance, bool wrongCount)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var called = false;
        var type = new WasmFunctionType([WasmValueKind.I32], []);
        WasmResults Callback(WasmExecutionContext callbackContext, ReadOnlySpan<WasmValue> values)
        {
            called = true;
            return new([]);
        }
        var function = withInstance
            ? WasmFunction.CreateHost(type, (callbackContext, _, values) => Callback(callbackContext, values))
            : WasmFunction.CreateHost(type, Callback);
        var instance = CreateHostAccessInstance(executionContext);
        WasmValue[] arguments = wrongCount ? [] : [WasmValue.FromI64(42)];

        // Act & Assert
        var exception = await Assert
            .That(() =>
                withInstance
                    ? function.Invoke(executionContext, instance, arguments)
                    : function.Invoke(executionContext, arguments)
            )
            .ThrowsExactly<ArgumentException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.ParamName).IsEqualTo("arguments");
            await Assert.That(called).IsFalse();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 単独hostからWasmを順に呼ぶ_各入口の上限を選びホストへ戻るたびにcontextを解除する(bool withInstance)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var access = CreateHostAccessInstance(executionContext);
        var a = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 7, 0x0B), WasmSpecVersion.Core2)
            .Validate()
            .Instantiate(executionContext, [], new(1))
            .GetFunction("run");
        var b = CreateTwoCallInstance(executionContext, 2).GetFunction("run");
        var clearedBetweenCalls = false;
        WasmResults Callback(WasmExecutionContext callbackContext, ReadOnlySpan<WasmValue> values)
        {
            var first = a.Invoke(callbackContext, []);
            clearedBetweenCalls = callbackContext.Current is null;
            var second = b.Invoke(callbackContext, []);
            clearedBetweenCalls &= callbackContext.Current is null;
            return new([first.Values[0], second.Values[0]]);
        }
        var type = new WasmFunctionType([], [WasmValueKind.I32, WasmValueKind.I32]);
        var host = withInstance
            ? WasmFunction.CreateHost(type, (callbackContext, _, values) => Callback(callbackContext, values))
            : WasmFunction.CreateHost(type, Callback);

        // Act
        var result = withInstance ? host.Invoke(executionContext, access, []) : host.Invoke(executionContext, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(7);
            await Assert.That(result.Values[1].AsI32()).IsEqualTo(42);
            await Assert.That(clearedBetweenCalls).IsTrue();
        }
    }

    private static WasmInstance CreateTwoCallInstance(WasmExecutionContext executionContext, int maxCallDepth)
    {
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 1, 0x0B]), ([], [0x41, 42, 0x0B]))
                ),
                WasmSpecVersion.Core2
            )
            .Validate()
            .Instantiate(executionContext, [], new(maxCallDepth));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 単独hostを両形式で呼ぶ_指定instanceとcontextを渡し実行状態を作らず結果を返す(bool withInstance)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var first = CreateHostAccessInstance(executionContext);
        var second = CreateHostAccessInstance(executionContext);
        List<WasmInstance> targets = [];
        var noContext = true;
        var sameContext = true;
        WasmResults Callback(WasmExecutionContext callbackContext, WasmInstance target, ReadOnlySpan<WasmValue> values)
        {
            sameContext &= ReferenceEquals(callbackContext, executionContext);
            targets.Add(target);
            target.GetMemory("memory").Write(0, [(byte)values[0].AsI32()]);
            noContext &= callbackContext.Current is null;
            return new(values);
        }
        var type = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I32]);
        var function = withInstance
            ? WasmFunction.CreateHost(type, Callback)
            : WasmFunction.CreateHost(type, (callbackContext, values) => Callback(callbackContext, first, values));

        // Act
        var a = withInstance
            ? function.Invoke(executionContext, first, [WasmValue.FromI32(42)])
            : function.Invoke(executionContext, [WasmValue.FromI32(42)]);
        var b = function.Invoke(executionContext, second, [WasmValue.FromI32(7)]);
        if (!withInstance)
        {
            function.Invoke(executionContext, null!, [WasmValue.FromI32(9)]);
        }
        byte[] buffer = [0];
        (withInstance ? second : first).GetMemory("memory").Read(0, buffer);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(noContext).IsTrue();
            await Assert.That(sameContext).IsTrue();
            await Assert.That(targets[0]).IsSameReferenceAs(first);
            await Assert.That(targets[1]).IsSameReferenceAs(withInstance ? second : first);
            await Assert.That(a.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(b.Values[0].AsI32()).IsEqualTo(7);
            await Assert.That(buffer[0]).IsEqualTo(withInstance ? (byte)7 : (byte)9);
        }
    }

    private static WasmInstance CreateHostAccessInstance(WasmExecutionContext executionContext)
    {
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Memories((1, 1)),
                    HostLinkingModuleBinary.Exports(("memory", 2, 0))
                ),
                WasmSpecVersion.Core2
            )
            .Validate()
            .Instantiate(executionContext, [], new(1));
    }
}
