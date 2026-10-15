using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task 定義関数を再exportして別instanceから呼ぶ_所属元のメモリとデータの内容と破棄状態を使う()
    {
        // Arrange
        var module = WasmModule.Decode(CreateDataInitializationModule()).Validate();
        var source = module.Instantiate([]);
        var sibling = module.Instantiate([]);
        source.GetMemory("memory").Write(0, [0xA1]);
        sibling.GetMemory("memory").Write(0, [0xC3]);
        sibling.GetFunction("drop").Invoke([]);
        var provider = new WasmHostModule("source");
        provider.Define("init", source.GetFunction("init"));
        provider.Define("drop", source.GetFunction("drop"));
        var relay = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F, 0x7F, 0x7F], []), ([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("source", "init", 0, [0]),
                        ("source", "drop", 0, [1])
                    ),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Memories((1, null)),
                    HostLinkingModuleBinary.Exports(
                        ("init", 0, 0),
                        ("drop", 0, 1),
                        ("run", 0, 2),
                        ("dropOwn", 0, 3),
                        ("memory", 2, 0)
                    ),
                    HostLinkingModuleBinary.Section(12, [2]),
                    HostLinkingModuleBinary.Code(
                        ([], [0x20, 0, 0x20, 1, 0x20, 2, 0x10, 0, 0x0B]),
                        ([], [0xFC, 9, 1, 0x0B])
                    ),
                    HostLinkingModuleBinary.Section(11, [2, 1, 1, 0xCC, 1, 1, 0xDD])
                )
            )
            .Validate()
            .Instantiate([provider]);
        relay.GetMemory("memory").Write(0, [0xB2]);
        relay.GetFunction("dropOwn").Invoke([]);

        // Act
        relay
            .GetFunction("init")
            .Invoke(relay, [WasmValue.FromI32(4), WasmValue.FromI32(0), WasmValue.FromI32(4)]);
        relay
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(8), WasmValue.FromI32(1), WasmValue.FromI32(2)]);
        relay.GetFunction("drop").Invoke([]);
        var sourceBytes = new byte[10];
        var relayBytes = new byte[10];
        var siblingBytes = new byte[10];
        source.GetMemory("memory").Read(0, sourceBytes);
        relay.GetMemory("memory").Read(0, relayBytes);
        sibling.GetMemory("memory").Read(0, siblingBytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(relay.GetFunction("init"))
                .IsSameReferenceAs(source.GetFunction("init"));
            await Assert
                .That(relay.GetFunction("drop"))
                .IsSameReferenceAs(source.GetFunction("drop"));
            await Assert.That(Convert.ToHexString(sourceBytes)).IsEqualTo("A1000000112233442233");
            await Assert.That(Convert.ToHexString(relayBytes)).IsEqualTo("B2000000000000000000");
            await Assert.That(Convert.ToHexString(siblingBytes)).IsEqualTo("C3000000000000000000");
        }

        // Act & Assert
        var trap = await Assert
            .That(() =>
                source
                    .GetFunction("init")
                    .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(1)])
            )
            .ThrowsExactly<WasmTrapException>();
        await Assert.That(trap!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task 共有メモリを拡張して同期再入する_正常終了とtrapとhost例外と深さ上限の後も外側と次の実行を保つ(
        int outcome
    )
    {
        // Arrange
        var expected = new InvalidOperationException("再入先のホスト例外");
        WasmFunction inner = null!;
        List<Exception> caught = [];
        List<WasmInstance> outerCallers = [];
        List<WasmInstance> innerCallers = [];
        var copy = new byte[1];
        var grown = false;
        uint previousPages = 0;
        var outerProvider = new WasmHostModule("env");
        outerProvider.Define(
            "reenter",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    outerCallers.Add(instance);
                    var memory = instance.GetMemory("memory");
                    if (memory.PageCount == 1)
                    {
                        memory.Read(0, copy);
                        grown = memory.TryGrow(1, out previousPages);
                    }
                    memory.Write(0, [9]);
                    memory.Write(65537, [0xAB]);
                    for (var i = 0; i < 2; i++)
                    {
                        try
                        {
                            inner.Invoke([]);
                        }
                        catch (Exception exception)
                        {
                            caught.Add(exception);
                        }
                    }
                    return new([]);
                }
            )
        );
        var outer = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([], []),
                        ([0x7F, 0x6F], [0x7F, 0x6F, 0x7F, 0x7F, 0x7F, 0x7F])
                    ),
                    HostLinkingModuleBinary.Imports(("env", "reenter", 0, [0])),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Memories((1, 2)),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("memory", 2, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, 0x7F)],
                            [
                                // localsと値スタックを残してhostへ入り、拡張後の領域を読む。
                                .. Convert.FromHexString("2000210241112001100020023F0041"),
                                .. HostLinkingModuleBinary.Signed(65536),
                                0x2D,
                                0,
                                0,
                                0x41,
                                .. HostLinkingModuleBinary.Signed(65537),
                                0x2D,
                                0,
                                0,
                                0x0B,
                            ]
                        )
                    ),
                    HostLinkingModuleBinary.Section(11, [1, 0, 0x41, 0, 0x0B, 1, 7])
                )
            )
            .Validate()
            .Instantiate([outerProvider], new(outcome == 3 ? 3 : 4));
        var innerProvider = new WasmHostModule("env");
        innerProvider.Define("m", outer.GetMemory("memory"));
        innerProvider.Define(
            "probe",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    innerCallers.Add(instance);
                    if (outcome == 2)
                    {
                        throw expected;
                    }
                    return new([]);
                }
            )
        );
        var innerModule = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "probe", 0, [0]),
                        ("env", "m", 2, [1, 1, 2])
                    ),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("memory", 2, 0)),
                    HostLinkingModuleBinary.Section(12, [1]),
                    HostLinkingModuleBinary.Code(
                        (
                            [(2048, 0x7F)],
                            [
                                // スタックを拡張し、パッシブデータを共有メモリへ書き込む。
                                0x41,
                                .. HostLinkingModuleBinary.Signed(65536),
                                .. Convert.FromHexString("41004101FC0800001000"),
                                .. outcome == 1
                                    ? (byte[])
                                        [
                                            0x41,
                                            .. HostLinkingModuleBinary.Signed(131072),
                                            0x2D,
                                            0,
                                            0,
                                            0x1A,
                                        ]
                                    : [],
                                0x0B,
                            ]
                        )
                    ),
                    HostLinkingModuleBinary.Section(11, [1, 1, 1, 0xB6])
                )
            )
            .Validate();
        var innerInstance = innerModule.Instantiate([innerProvider], new(2));
        inner = innerInstance.GetFunction("run");
        var limited = innerModule.Instantiate([innerProvider], new(1));
        var firstReference = new object();
        var nextReference = new object();

        // Act
        var first = outer
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(42), WasmValue.FromExternRef(firstReference)]);

        // Act & Assert
        var independentExhaustion = await Assert
            .That(() => limited.GetFunction("run").Invoke([]))
            .ThrowsExactly<WasmExhaustionException>();
        var next = outer
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(84), WasmValue.FromExternRef(nextReference)]);
        var current = new byte[1];
        var added = new byte[2];
        outer.GetMemory("memory").Read(0, current);
        innerInstance.GetMemory("memory").Read(65536, added);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(grown).IsTrue();
            await Assert.That(previousPages).IsEqualTo(1u);
            await Assert.That(copy[0]).IsEqualTo((byte)7);
            await Assert.That(current[0]).IsEqualTo((byte)9);
            await Assert.That(Convert.ToHexString(added)).IsEqualTo("B6AB");
            await Assert
                .That(innerInstance.GetMemory("memory"))
                .IsSameReferenceAs(outer.GetMemory("memory"));
            await Assert
                .That(
                    first.Values.SequenceEqual([
                        WasmValue.FromI32(17),
                        WasmValue.FromExternRef(firstReference),
                        WasmValue.FromI32(42),
                        WasmValue.FromI32(2),
                        WasmValue.FromI32(0xB6),
                        WasmValue.FromI32(0xAB),
                    ])
                )
                .IsTrue();
            await Assert
                .That(
                    next.Values.SequenceEqual([
                        WasmValue.FromI32(17),
                        WasmValue.FromExternRef(nextReference),
                        WasmValue.FromI32(84),
                        WasmValue.FromI32(2),
                        WasmValue.FromI32(0xB6),
                        WasmValue.FromI32(0xAB),
                    ])
                )
                .IsTrue();
            await Assert
                .That(independentExhaustion!.Reason)
                .IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(independentExhaustion.Limit).IsEqualTo(1);
            await Assert.That(outerCallers.Count).IsEqualTo(2);
            foreach (var caller in outerCallers)
            {
                await Assert.That(caller).IsSameReferenceAs(outer);
            }
            await Assert.That(innerCallers.Count).IsEqualTo(outcome == 3 ? 0 : 4);
            foreach (var caller in innerCallers)
            {
                await Assert.That(caller).IsSameReferenceAs(innerInstance);
            }
            await Assert.That(caught.Count).IsEqualTo(outcome == 0 ? 0 : 4);
            foreach (var exception in caught)
            {
                if (outcome == 1)
                {
                    await Assert.That(exception).IsTypeOf<WasmTrapException>();
                    var trap = (WasmTrapException)exception;
                    await Assert.That(trap.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
                    await Assert.That(trap.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
                }
                else if (outcome == 2)
                {
                    await Assert.That(exception).IsSameReferenceAs(expected);
                }
                else
                {
                    await Assert.That(exception).IsTypeOf<WasmExhaustionException>();
                    var exhaustion = (WasmExhaustionException)exception;
                    await Assert
                        .That(exhaustion.Reason)
                        .IsEqualTo(WasmExhaustionReason.CallDepthLimit);
                    await Assert.That(exhaustion.Limit).IsEqualTo(3);
                }
            }
        }
    }
}
