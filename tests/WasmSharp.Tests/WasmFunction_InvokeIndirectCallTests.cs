using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 別moduleの同じ構造の型を間接callする_複数引数結果の順序と元instanceのリソースを保つ()
    {
        // Arrange
        var callee = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F, 0x7E], [0x7E, 0x7F, 0x7F, 0x7F, 0x70])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Tables((0x70, 1, null)),
                    HostLinkingModuleBinary.Memories((1, null)),
                    HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 42, 0x0B])),
                    HostLinkingModuleBinary.Exports(("f", 0, 0), ("t", 1, 0), ("m", 2, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [0x20, 1, 0x20, 0, 0x23, 0, 0x41, 0, 0x2D, 0, 0, 0x41, 0, 0x25, 0, 0x0B]
                        )
                    )
                )
            )
            .Validate()
            .Instantiate([]);
        var marker = WasmFunction.CreateHost(new([], []), _ => new([]));
        callee.GetTable("t").Set(0, WasmValue.FromFuncRef(marker));
        callee.GetMemory("m").Write(0, [77]);
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        table.Set(0, WasmValue.FromFuncRef(callee.GetFunction("f")));
        var host = new WasmHostModule("env");
        host.Define("t", table);
        var callerModule = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([], []),
                        ([0x7F, 0x7E], [0x7E, 0x7F, 0x7F, 0x7F, 0x70]),
                        ([0x7F, 0x7E, 0x7F], [0x7E, 0x7F, 0x7F, 0x7F, 0x70])
                    ),
                    HostLinkingModuleBinary.Imports(
                        ("env", "unused", 1, [0x6F, 0, 1]),
                        ("env", "t", 1, [0x70, 0, 1])
                    ),
                    HostLinkingModuleBinary.Functions(2),
                    HostLinkingModuleBinary.Memories((1, null)),
                    HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        ([], [0x20, 0, 0x20, 1, 0x20, 2, 0x11, 1, 1, 0x0B])
                    )
                )
            )
            .Validate();
        host.Define("unused", new WasmTable(WasmValueKind.ExternRef, new(1)));
        var caller = callerModule.Instantiate([host]);

        // Act
        var values = caller
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(7), WasmValue.FromI64(9), WasmValue.FromI32(0)])
            .Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    values.SequenceEqual([
                        WasmValue.FromI64(9),
                        WasmValue.FromI32(7),
                        WasmValue.FromI32(42),
                        WasmValue.FromI32(77),
                        WasmValue.FromFuncRef(marker),
                    ])
                )
                .IsTrue();
            await Assert
                .That(ReferenceEquals(callerModule.Types[1], callee.GetFunction("f").Type))
                .IsFalse();
            await Assert.That(table.Get(0).AsFuncRef()).IsSameReferenceAs(callee.GetFunction("f"));
        }
    }

    [Test]
    [Arguments(-1, WasmTrapReason.TableOutOfBounds, "undefined element -1")]
    [Arguments(3, WasmTrapReason.TableOutOfBounds, "undefined element 3")]
    [Arguments(0, WasmTrapReason.UninitializedElement, "uninitialized element 0")]
    [Arguments(1, WasmTrapReason.IndirectCallTypeMismatch, "indirect call type mismatch")]
    [Arguments(2, WasmTrapReason.IndirectCallTypeMismatch, "indirect call type mismatch")]
    public async Task 間接callの選択が範囲外かnullか引数結果の型不一致_callback前に元命令位置でtrapになり再呼び出しできる(
        int selector,
        WasmTrapReason reason,
        string prefix
    )
    {
        // Arrange
        var calls = 0;
        var table = new WasmTable(WasmValueKind.FuncRef, new(3));
        table.Set(
            1,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    new([WasmValueKind.I32], []),
                    _ =>
                    {
                        calls++;
                        return new([]);
                    }
                )
            )
        );
        table.Set(
            2,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    new([], [WasmValueKind.I32]),
                    _ =>
                    {
                        calls++;
                        return new([WasmValue.FromI32(1)]);
                    }
                )
            )
        );
        var (module, instance) = CreateIndirectVoidCaller(table, maxCallDepth: 1);
        var run = instance.GetFunction("run");

        // Act & Assert
        var exception = await Assert
            .That(() => run.Invoke([WasmValue.FromI32(selector)]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(reason);
            await Assert
                .That(exception.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Invoke,
                        module.Functions[0].Instructions[1].ByteOffset,
                        0
                    )
                );
            await Assert.That(calls).IsEqualTo(0);
        }
        await Assert.That(() => instance.GetFunction("ok").Invoke([])).ThrowsNothing();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task tableからinstance必須hostを間接callする_呼び出し元instanceを渡し例外後も状態を復元する(
        bool throws
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var (_, instance) = CreateIndirectVoidCaller(table);
        WasmInstance? observed = null;
        var expected = new InvalidOperationException("host failure");
        table.Set(
            0,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    new([], []),
                    (caller, _) =>
                    {
                        observed = caller;
                        if (throws)
                        {
                            throw expected;
                        }
                        return new([]);
                    }
                )
            )
        );

        if (throws)
        {
            // Act & Assert
            var actual = await Assert
                .That(() => instance.GetFunction("run").Invoke([WasmValue.FromI32(0)]))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(actual).IsSameReferenceAs(expected);
        }
        else
        {
            // Act
            instance.GetFunction("run").Invoke([WasmValue.FromI32(0)]);
        }

        // Assert
        await Assert.That(observed).IsSameReferenceAs(instance);
        await Assert.That(() => instance.GetFunction("ok").Invoke([])).ThrowsNothing();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 間接callから再帰またはhost同期再入する_共通深さ上限でexhaustionになり後続の呼び出しを復元する(
        bool hostReentry
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var host = new WasmHostModule("env");
        host.Define("t", table);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "t", 1, [0x70, 0, 1])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("ok", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x41, 0, 0x11, 0, 0, 0x0B]), ([], [0x0B]))
                )
            )
            .Validate();
        var instance = module.Instantiate([host], new(3));
        var calls = 0;
        var run = instance.GetFunction("run");
        table.Set(
            0,
            WasmValue.FromFuncRef(
                hostReentry
                    ? WasmFunction.CreateHost(
                        new([], []),
                        _ =>
                        {
                            calls++;
                            return run.Invoke([]);
                        }
                    )
                    : run
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => run.Invoke([]))
            .ThrowsExactly<WasmExhaustionException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(calls).IsEqualTo(hostReentry ? 1 : 0);
        }
        table.Set(0, WasmValue.FromFuncRef(instance.GetFunction("ok")));
        await Assert.That(() => run.Invoke([])).ThrowsNothing();
        await Assert.That(() => run.Invoke([])).ThrowsNothing();
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(5)]
    public async Task host同期再入の間接callが失敗する_外側の上限と状態を復元して同じcallbackから再呼び出しできる(
        int selector
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(6));
        var host = new WasmHostModule("env");
        host.Define("t", table);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([0x7F], [])),
                    HostLinkingModuleBinary.Imports(("env", "t", 1, [0x70, 0, 6])),
                    HostLinkingModuleBinary.Functions(1, 0, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("trap", 0, 1), ("loop", 0, 2)),
                    HostLinkingModuleBinary.Code(
                        ([], [0x20, 0, 0x11, 0, 0, 0x0B]),
                        ([], [0x00, 0x0B]),
                        ([], [0x41, 5, 0x11, 0, 0, 0x0B])
                    )
                )
            )
            .Validate();
        var outer = module.Instantiate([host], new(4));
        var inner = module.Instantiate([host], new(1));
        var expected = new InvalidOperationException("nested host failure");
        Exception? caught = null;
        WasmInstance? observed = null;
        var successes = 0;
        table.Set(
            0,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    new([], []),
                    _ =>
                    {
                        try
                        {
                            inner.GetFunction("run").Invoke([WasmValue.FromI32(selector)]);
                        }
                        catch (Exception exception)
                        {
                            caught = exception;
                        }
                        inner.GetFunction("run").Invoke([WasmValue.FromI32(4)]);
                        return new([]);
                    }
                )
            )
        );
        table.Set(2, WasmValue.FromFuncRef(inner.GetFunction("trap")));
        table.Set(
            3,
            WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => throw expected))
        );
        table.Set(
            4,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    new([], []),
                    (caller, _) =>
                    {
                        observed = caller;
                        successes++;
                        return new([]);
                    }
                )
            )
        );
        table.Set(5, WasmValue.FromFuncRef(inner.GetFunction("loop")));

        // Act
        outer.GetFunction("run").Invoke([WasmValue.FromI32(0)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(successes).IsEqualTo(1);
            await Assert.That(observed).IsSameReferenceAs(inner);
            await Assert
                .That(caught?.GetType())
                .IsEqualTo(
                    selector switch
                    {
                        3 => typeof(InvalidOperationException),
                        5 => typeof(WasmExhaustionException),
                        _ => typeof(WasmTrapException),
                    }
                );
            if (selector == 3)
            {
                await Assert.That(caught).IsSameReferenceAs(expected);
            }
        }
        await Assert
            .That(() => inner.GetFunction("run").Invoke([WasmValue.FromI32(4)]))
            .ThrowsExactly<WasmExhaustionException>();
        await Assert.That(successes).IsEqualTo(1);
        await Assert
            .That(() => outer.GetFunction("run").Invoke([WasmValue.FromI32(4)]))
            .ThrowsNothing();
        await Assert.That(successes).IsEqualTo(2);
        await Assert.That(observed).IsSameReferenceAs(outer);
    }

    private static (WasmModule Module, WasmInstance Instance) CreateIndirectVoidCaller(
        WasmTable table,
        int maxCallDepth = 8
    )
    {
        var host = new WasmHostModule("env");
        host.Define("t", table);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([0x7F], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "t", 1, [0x70, .. HostLinkingModuleBinary.Limits(table.Count)])
                    ),
                    HostLinkingModuleBinary.Functions(1, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("ok", 0, 1)),
                    HostLinkingModuleBinary.Code(([], [0x20, 0, 0x11, 0, 0, 0x0B]), ([], [0x0B]))
                )
            )
            .Validate();
        return (module, module.Instantiate([host], new(maxCallDepth)));
    }
}
