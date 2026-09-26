using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Hostから同期再入してstackを拡張する_正常終了と捕捉したtrapの後も引数と外側localsを保つ(
        bool sameInstance,
        bool trap
    )
    {
        // Arrange
        var reference = new object();
        WasmFunction inner = null!;
        WasmInstance? target = null;
        WasmTrapException? caught = null;
        (int Frames, int Values, int Depth) afterReentry = default;
        var contextPreserved = false;
        var host = WasmFunction.CreateHost(
            new(
                [WasmValueKind.I32, WasmValueKind.ExternRef],
                [WasmValueKind.I32, WasmValueKind.ExternRef]
            ),
            (instance, values) =>
            {
                target = instance;
                var context = InterpreterContext.Current!;
                try
                {
                    inner.Invoke([]);
                }
                catch (WasmTrapException exception)
                {
                    caught = exception;
                }
                afterReentry = (context.FrameCount, context.ValueCount, context.CallDepth);
                contextPreserved = ReferenceEquals(context, InterpreterContext.Current);
                return new(values);
            }
        );
        var provider = new WasmHostModule("env");
        provider.Define("host", host);
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([0x7F, 0x6F], [0x7F, 0x6F]),
                        ([0x7F, 0x6F], [0x7F, 0x6F, 0x7F]),
                        ([], [0x7F])
                    ),
                    HostLinkingModuleBinary.Imports(("env", "host", 0, [0])),
                    HostLinkingModuleBinary.Functions(1, 2, 2),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("inner", 0, 2)),
                    HostLinkingModuleBinary.Code(
                        ([(1, 0x7F)], [0x41, 7, 0x21, 2, 0x20, 0, 0x20, 1, 0x10, 0, 0x20, 2, 0x0B]),
                        ([(2048, 0x6F)], trap ? [0x00, 0x0B] : [0x10, 3, 0x0B]),
                        ([], [0x41, 9, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([provider], new(4));
        inner = sameInstance
            ? instance.GetFunction("inner")
            : WasmModule
                .Decode(
                    HostLinkingModuleBinary.Create(
                        HostLinkingModuleBinary.Types(([], [0x7F])),
                        HostLinkingModuleBinary.Functions(0, 0),
                        HostLinkingModuleBinary.Exports(("run", 0, 0)),
                        HostLinkingModuleBinary.Code(
                            ([(2048, 0x6F)], trap ? [0x00, 0x0B] : [0x10, 1, 0x0B]),
                            ([], [0x41, 9, 0x0B])
                        )
                    )
                )
                .Validate()
                .Instantiate([], new(1))
                .GetFunction("run");

        // Act
        var result = instance
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(42), WasmValue.FromExternRef(reference)]);
        var cleared = InterpreterContext.Current is null;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(target).IsSameReferenceAs(instance);
            await Assert.That(caught is not null).IsEqualTo(trap);
            await Assert.That(afterReentry).IsEqualTo((1, 5, 2));
            await Assert.That(contextPreserved).IsTrue();
            await Assert.That(cleared).IsTrue();
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            await Assert.That(result.Values[1].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(result.Values[2].AsI32()).IsEqualTo(7);
        }
    }

    [Test]
    public async Task 定義関数と再exportしたhostを別instanceから呼ぶ_直前の定義関数の所属instanceを渡す()
    {
        // Arrange
        List<WasmInstance> observed = [];
        var host = WasmFunction.CreateHost(
            new([], []),
            (instance, _) =>
            {
                observed.Add(instance);
                return new([]);
            }
        );
        var source = CreateHostCallingInstance(host, 2);
        var throughDefinition = CreateHostCallingInstance(source.GetFunction("run"), 3);
        var throughReexport = CreateHostCallingInstance(source.GetFunction("host"), 2);

        // Act
        throughDefinition.GetFunction("run").Invoke([]);
        throughReexport.GetFunction("run").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(observed[0]).IsSameReferenceAs(source);
            await Assert.That(observed[1]).IsSameReferenceAs(throughReexport);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 進行中のhostから別hostを公開Invokeする_同じcontextの深さへ数え終了時に復元する(
        bool withInstance
    )
    {
        // Arrange
        var nestedCalled = false;
        var nested = WasmFunction.CreateHost(
            new([], []),
            _ =>
            {
                nestedCalled = true;
                return new([]);
            }
        );
        WasmResults Callback(ReadOnlySpan<WasmValue> values)
        {
            return nested.Invoke([]);
        }

        var host = withInstance
            ? WasmFunction.CreateHost(new([], []), (_, values) => Callback(values))
            : WasmFunction.CreateHost(new([], []), Callback);
        var function = CreateHostCallingInstance(host, 2).GetFunction("run");
        var cleared = false;

        // Act & Assert
        var exception = await Assert
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
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Limit).IsEqualTo(2);
            await Assert.That(nestedCalled).IsFalse();
            await Assert.That(cleared).IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Guestからhostを繰り返し呼ぶ_上限到達時は実行せず正常終了ごとに深さを解放する(
        bool withInstance
    )
    {
        // Arrange
        var calls = 0;
        List<(int Frames, int Depth)> observed = [];
        WasmResults Callback(ReadOnlySpan<WasmValue> values)
        {
            calls++;
            var context = InterpreterContext.Current!;
            observed.Add((context.FrameCount, context.CallDepth));
            return new([]);
        }
        var host = withInstance
            ? WasmFunction.CreateHost(new([], []), (_, values) => Callback(values))
            : WasmFunction.CreateHost(new([], []), Callback);
        var limited = CreateHostCallingInstance(host, 1).GetFunction("run");
        var enough = CreateHostCallingInstance(host, 2, [0x10, 0, 0x10, 0, 0x0B])
            .GetFunction("run");
        var cleared = false;

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                try
                {
                    limited.Invoke([]);
                }
                finally
                {
                    cleared = InterpreterContext.Current is null;
                }
            })
            .ThrowsExactly<WasmExhaustionException>();
        await Assert.That(calls).IsEqualTo(0);
        enough.Invoke([]);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exception.Limit).IsEqualTo(1);
            await Assert.That(cleared).IsTrue();
            await Assert.That(calls).IsEqualTo(2);
            await Assert.That(observed.SequenceEqual([(1, 2), (1, 2)])).IsTrue();
        }
    }

    private static WasmInstance CreateHostCallingInstance(
        WasmFunction host,
        int maxCallDepth,
        byte[]? instructions = null
    )
    {
        var provider = new WasmHostModule("env");
        provider.Define("host", host);
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "host", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("host", 0, 0)),
                    HostLinkingModuleBinary.Code(([], instructions ?? [0x10, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([provider], new(maxCallDepth));
    }
}
