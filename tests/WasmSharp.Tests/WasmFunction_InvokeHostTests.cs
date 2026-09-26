using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 定義関数へnullまたは別instanceを指定する_指定を無視し定義元の上限で実行する(
        bool explicitNull
    )
    {
        // Arrange
        var source = CreateTwoCallInstance(2);
        var other = CreateHostAccessInstance();

        // Act
        var result = source.GetFunction("run").Invoke(explicitNull ? null! : other, []);
        var cleared = InterpreterContext.Current is null;

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
    public async Task Hostへ個数または型が異なる引数を渡す_両Invoke形式で実行前に拒否する(
        bool withInstance,
        bool wrongCount
    )
    {
        // Arrange
        var called = false;
        var type = new WasmFunctionType([WasmValueKind.I32], []);
        WasmResults Callback(ReadOnlySpan<WasmValue> values)
        {
            called = true;
            return new([]);
        }
        var function = withInstance
            ? WasmFunction.CreateHost(type, (_, values) => Callback(values))
            : WasmFunction.CreateHost(type, Callback);
        var instance = CreateHostAccessInstance();
        WasmValue[] arguments = wrongCount ? [] : [WasmValue.FromI64(42)];

        // Act & Assert
        var exception = await Assert
            .That(() =>
                withInstance ? function.Invoke(instance, arguments) : function.Invoke(arguments)
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
    public async Task 単独hostからWasmを順に呼ぶ_各入口の上限を選びホストへ戻るたびにcontextを解除する(
        bool withInstance
    )
    {
        // Arrange
        var access = CreateHostAccessInstance();
        var a = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 7, 0x0B))
            .Validate()
            .Instantiate([], new(1))
            .GetFunction("run");
        var b = CreateTwoCallInstance(2).GetFunction("run");
        var clearedBetweenCalls = false;
        WasmResults Callback(ReadOnlySpan<WasmValue> values)
        {
            var first = a.Invoke([]);
            clearedBetweenCalls = InterpreterContext.Current is null;
            var second = b.Invoke([]);
            clearedBetweenCalls &= InterpreterContext.Current is null;
            return new([first.Values[0], second.Values[0]]);
        }
        var type = new WasmFunctionType([], [WasmValueKind.I32, WasmValueKind.I32]);
        var host = withInstance
            ? WasmFunction.CreateHost(type, (_, values) => Callback(values))
            : WasmFunction.CreateHost(type, Callback);

        // Act
        var result = withInstance ? host.Invoke(access, []) : host.Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(7);
            await Assert.That(result.Values[1].AsI32()).IsEqualTo(42);
            await Assert.That(clearedBetweenCalls).IsTrue();
        }
    }

    private static WasmInstance CreateTwoCallInstance(int maxCallDepth)
    {
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x10, 1, 0x0B]), ([], [0x41, 42, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([], new(maxCallDepth));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 単独hostを両形式で呼ぶ_指定instanceを使いcontextを作らず結果を返す(
        bool withInstance
    )
    {
        // Arrange
        var first = CreateHostAccessInstance();
        var second = CreateHostAccessInstance();
        List<WasmInstance> targets = [];
        var noContext = true;
        WasmResults Callback(WasmInstance target, ReadOnlySpan<WasmValue> values)
        {
            targets.Add(target);
            target.GetMemory("memory").Write(0, [(byte)values[0].AsI32()]);
            noContext &= InterpreterContext.Current is null;
            return new(values);
        }
        var type = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.I32]);
        var function = withInstance
            ? WasmFunction.CreateHost(type, Callback)
            : WasmFunction.CreateHost(type, values => Callback(first, values));

        // Act
        var a = withInstance
            ? function.Invoke(first, [WasmValue.FromI32(42)])
            : function.Invoke([WasmValue.FromI32(42)]);
        var b = function.Invoke(second, [WasmValue.FromI32(7)]);
        if (!withInstance)
        {
            function.Invoke(null!, [WasmValue.FromI32(9)]);
        }
        byte[] buffer = [0];
        (withInstance ? second : first).GetMemory("memory").Read(0, buffer);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(noContext).IsTrue();
            await Assert.That(targets[0]).IsSameReferenceAs(first);
            await Assert.That(targets[1]).IsSameReferenceAs(withInstance ? second : first);
            await Assert.That(a.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(b.Values[0].AsI32()).IsEqualTo(7);
            await Assert.That(buffer[0]).IsEqualTo(withInstance ? (byte)7 : (byte)9);
        }
    }

    private static WasmInstance CreateHostAccessInstance()
    {
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Memories((1, 1)),
                    HostLinkingModuleBinary.Exports(("memory", 2, 0))
                )
            )
            .Validate()
            .Instantiate([], new(1));
    }
}
