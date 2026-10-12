using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false, 0, 22)]
    [Arguments(false, -3, 11)]
    [Arguments(true, 0, 22)]
    [Arguments(true, -3, 11)]
    public async Task selectの両候補と条件で共有globalとhostを更新する_非短絡で命令順に評価して選択値を返す(
        bool typed,
        int condition,
        int expected
    )
    {
        // Arrange
        var state = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        List<(int Argument, int State)> observed = [];
        var host = new WasmHostModule("env");
        host.Define("state", state);
        host.Define(
            "observe",
            WasmFunction.CreateHost(
                new([WasmValueKind.I32], [WasmValueKind.I32]),
                (instance, arguments) =>
                {
                    var shared = instance.GetGlobalResource("state");
                    observed.Add((arguments[0].AsI32(), shared.Value.AsI32()));
                    shared.Value = WasmValue.FromI32(shared.Value.AsI32() + 10);
                    return new(arguments);
                }
            )
        );
        byte[] select = typed ? [0x1C, 1, 0x7F] : [0x1B];
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7F]), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "observe", 0, [0]),
                        ("env", "state", 3, [0x7F, 1])
                    ),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("read", 0, 2), ("state", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [
                                // 第1候補、第2候補、条件の順にglobalへ1/2/3を加え、hostでさらに10を加える。
                                .. Convert.FromHexString("230041016A2400410B1000"),
                                .. Convert.FromHexString("230041026A240041161000"),
                                .. Convert.FromHexString("230041036A240020001000"),
                                .. select,
                                0x0B,
                            ]
                        ),
                        ([], [0x23, 0, 0x0B])
                    )
                )
            )
            .Validate();
        var instance = module.Instantiate([host]);
        var other = module.Instantiate([host]);

        // Act
        var result = instance.GetFunction("run").Invoke([WasmValue.FromI32(condition)]);
        var later = other.GetFunction("read").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(expected);
            await Assert
                .That(observed.SequenceEqual([(11, 1), (22, 13), (condition, 26)]))
                .IsTrue();
            await Assert.That(later.Values[0].AsI32()).IsEqualTo(36);
            await Assert.That(instance.GetGlobalResource("state")).IsSameReferenceAs(state);
            await Assert.That(other.GetGlobalResource("state")).IsSameReferenceAs(state);
            await Assert.That(state.Type.ValueKind).IsEqualTo(WasmValueKind.I32);
            await Assert.That(state.Type.IsMutable).IsTrue();
        }
    }

    [Test]
    [Arguments(0, 4, 11)]
    [Arguments(-1, 3, 7)]
    public async Task brとifで共有globalへの更新を飛び越える_実行した側だけが別instanceの後続呼出しから見える(
        int condition,
        int expectedMarker,
        int expectedState
    )
    {
        // Arrange
        var state = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(1));
        var constant = new WasmGlobal(new(WasmValueKind.I64, false), WasmValue.FromI64(5));
        List<(int Marker, int State)> observed = [];
        var host = new WasmHostModule("env");
        host.Define("state", state);
        host.Define("constant", constant);
        host.Define(
            "observe",
            WasmFunction.CreateHost(
                new([WasmValueKind.I32], []),
                arguments =>
                {
                    observed.Add((arguments[0].AsI32(), state.Value.AsI32()));
                    return new([]);
                }
            )
        );
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], []), ([], [0x7F, 0x7E])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "observe", 0, [0]),
                        ("env", "state", 3, [0x7F, 1]),
                        ("env", "constant", 3, [0x7E, 0])
                    ),
                    HostLinkingModuleBinary.Functions(0, 1),
                    HostLinkingModuleBinary.Exports(
                        ("run", 0, 1),
                        ("read", 0, 2),
                        ("state", 3, 0),
                        ("constant", 3, 1)
                    ),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [
                                // block内で2を加えて記録し、brで40の加算とmarker2を飛び越える。
                                .. Convert.FromHexString("0240230041026A2400410110000C00"),
                                .. Convert.FromHexString("230041286A2400410210000B"),
                                // ifの選択側だけで4または8を加えて記録する。
                                .. Convert.FromHexString("20000440230041046A240041031000"),
                                .. Convert.FromHexString("05230041086A2400410410000B0B"),
                            ]
                        ),
                        ([], [0x23, 0, 0x23, 1, 0x0B])
                    )
                )
            )
            .Validate();
        var instance = module.Instantiate([host]);
        var other = module.Instantiate([host]);

        // Act
        instance.GetFunction("run").Invoke([WasmValue.FromI32(condition)]);
        var later = other.GetFunction("read").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(observed.SequenceEqual([(1, 3), (expectedMarker, expectedState)]))
                .IsTrue();
            await Assert.That(later.Values[0].AsI32()).IsEqualTo(expectedState);
            await Assert.That(later.Values[1].AsI64()).IsEqualTo(5L);
            await Assert
                .That(instance.GetGlobalResource("state"))
                .IsSameReferenceAs(other.GetGlobalResource("state"));
            await Assert.That(other.GetGlobalResource("constant")).IsSameReferenceAs(constant);
            await Assert.That(constant.Type.ValueKind).IsEqualTo(WasmValueKind.I64);
            await Assert.That(constant.Type.IsMutable).IsFalse();
        }
    }

    [Test]
    public async Task 数値条件の制御からimport定義関数と再exportしたhostを呼ぶ_同じ関数実体と所属instanceを保つ()
    {
        // Arrange
        List<(WasmInstance Instance, int Argument, int State)> observed = [];
        var callback = WasmFunction.CreateHost(
            new([WasmValueKind.I32], [WasmValueKind.I32]),
            (instance, arguments) =>
            {
                var state = instance.GetGlobalResource("state");
                observed.Add((instance, arguments[0].AsI32(), state.Value.AsI32()));
                state.Value = WasmValue.FromI32(state.Value.AsI32() + 1);
                return new([WasmValue.FromI32(arguments[0].AsI32() + 10)]);
            }
        );
        var host = new WasmHostModule("env");
        host.Define("observe", callback);
        var source = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "observe", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 7, 0x0B])),
                    HostLinkingModuleBinary.Exports(("f", 0, 1), ("h", 0, 0), ("state", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            // 正の引数なら所属instanceのglobalに加え、同じinstanceをhostへ渡す。
                            Convert.FromHexString("200041004A047F230020006A24002300100005000B0B")
                        )
                    )
                )
            )
            .Validate()
            .Instantiate([host]);
        var provider = new WasmHostModule("source");
        provider.Define("f", source.GetFunction("f"));
        provider.Define("h", source.GetFunction("h"));
        var destination = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F], [0x7F]), ([], [0x7F, 0x7F])),
                    HostLinkingModuleBinary.Imports(
                        ("source", "f", 0, [0]),
                        ("source", "h", 0, [0])
                    ),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Globals(
                        (0x7F, true, [0x41, .. HostLinkingModuleBinary.Signed(100), 0x0B])
                    ),
                    HostLinkingModuleBinary.Exports(
                        ("run", 0, 2),
                        ("f", 0, 0),
                        ("h", 0, 1),
                        ("state", 3, 0)
                    ),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            // 3+4==7のifからf(3)を呼び、続いて2*3を再exportされたhostへ渡す。
                            Convert.FromHexString(
                                "410341046A410746047F4103100005000B410241036C10010B"
                            )
                        )
                    )
                )
            )
            .Validate()
            .Instantiate([provider]);

        // Act
        var result = destination.GetFunction("run").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(result.Values.Select(x => x.AsI32()).SequenceEqual([20, 16]))
                .IsTrue();
            await Assert.That(observed.Count).IsEqualTo(2);
            await Assert.That(observed[0].Instance).IsSameReferenceAs(source);
            await Assert.That(observed[1].Instance).IsSameReferenceAs(destination);
            await Assert
                .That(
                    observed.Select(x => (x.Argument, x.State)).SequenceEqual([(10, 10), (6, 100)])
                )
                .IsTrue();
            await Assert.That(source.GetGlobal("state").AsI32()).IsEqualTo(11);
            await Assert.That(destination.GetGlobal("state").AsI32()).IsEqualTo(101);
            await Assert
                .That(destination.GetFunction("f"))
                .IsSameReferenceAs(source.GetFunction("f"));
            await Assert.That(destination.GetFunction("h")).IsSameReferenceAs(callback);
            await Assert.That(source.GetFunction("h")).IsSameReferenceAs(callback);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 数値条件のif内でhostが例外を投げる_型と元実体を保ち後続更新とcallbackを実行しない(
        bool runtimeTrap
    )
    {
        // Arrange
        Exception expected = runtimeTrap
            ? new WasmTrapException(
                "host側のtrap",
                WasmTrapReason.IntegerOverflow,
                new(WasmProcessingStage.Instantiate, 123, 456)
            )
            : new InvalidOperationException("host側の失敗");
        var state = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(2));
        WasmInstance? observed = null;
        var afterCalled = false;
        var host = new WasmHostModule("env");
        host.Define("state", state);
        host.Define(
            "fail",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    observed = instance;
                    throw expected;
                }
            )
        );
        host.Define(
            "after",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    afterCalled = true;
                    return new([]);
                }
            )
        );
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([0x7F], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "fail", 0, [0]),
                        ("env", "after", 0, [0]),
                        ("env", "state", 3, [0x7F, 1])
                    ),
                    HostLinkingModuleBinary.Functions(1, 2),
                    HostLinkingModuleBinary.Exports(("run", 0, 2), ("read", 0, 3)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            // 引数+1が非ゼロなら7を加えてhostを呼び、その後の加算とcallbackを飛ばす。
                            Convert.FromHexString(
                                "200041016A0440230041076A24001000230041016A240010010B0B"
                            )
                        ),
                        ([], [0x23, 0, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([host]);
        var function = instance.GetFunction("run");

        // Act & Assert
        Exception? actual = runtimeTrap
            ? await Assert
                .That(() => function.Invoke([WasmValue.FromI32(0)]))
                .ThrowsExactly<WasmTrapException>()
            : await Assert
                .That(() => function.Invoke([WasmValue.FromI32(0)]))
                .ThrowsExactly<InvalidOperationException>();
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsSameReferenceAs(expected);
            await Assert.That(observed).IsSameReferenceAs(instance);
            await Assert
                .That(instance.GetFunction("read").Invoke([]).Values[0].AsI32())
                .IsEqualTo(9);
            await Assert.That(afterCalled).IsFalse();
        }
    }

    [Test]
    [Arguments("410741006D", WasmTrapReason.IntegerDivideByZero, "integer divide by zero")]
    [Arguments("00", WasmTrapReason.Unreachable, "unreachable executed")]
    public async Task selectで選ばれない第2候補にtrapがある_候補計算で中断し条件以後の副作用を実行しない(
        string trapInstructions,
        WasmTrapReason reason,
        string message
    )
    {
        // Arrange
        var state = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        List<int> observed = [];
        var host = new WasmHostModule("env");
        host.Define("state", state);
        host.Define(
            "observe",
            WasmFunction.CreateHost(
                new([WasmValueKind.I32], [WasmValueKind.I32]),
                arguments =>
                {
                    var marker = arguments[0].AsI32();
                    observed.Add(marker);
                    state.Value = WasmValue.FromI32(state.Value.AsI32() + marker);
                    return new(arguments);
                }
            )
        );
        byte[] afterTrap = Convert.FromHexString("410310001B1A410410001A0B");
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], [0x7F]), ([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(
                ("env", "observe", 0, [0]),
                ("env", "state", 3, [0x7F, 1])
            ),
            HostLinkingModuleBinary.Functions(2, 1),
            HostLinkingModuleBinary.Exports(("run", 0, 2), ("read", 0, 1)),
            HostLinkingModuleBinary.Code(
                ([], [0x23, 0, 0x0B]),
                (
                    [],
                    [
                        // 両候補の記録後に第2候補でtrapする。条件3なら本来は第1候補を選ぶ。
                        .. Convert.FromHexString("41011000410210001A"),
                        .. Convert.FromHexString(trapInstructions),
                        .. afterTrap,
                    ]
                )
            )
        );
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([host]);
        var expectedOffset = bytes.Length - afterTrap.Length - 1;

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetFunction("run").Invoke([]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(reason);
            await Assert
                .That(exception.Message.StartsWith(message, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Invoke, expectedOffset, 2));
            await Assert.That(observed.SequenceEqual([1, 2])).IsTrue();
            await Assert
                .That(instance.GetFunction("read").Invoke([]).Values[0].AsI32())
                .IsEqualTo(3);
        }
    }

    [Test]
    [Arguments(false, 0, 0x7FC54321U, 0x7FF8ABCDEF012345UL, 0U, 0UL)]
    [Arguments(false, -1, 0xFF812345U, 0xFFF0123456789ABCUL, 0x80000000U, 0x8000000000000000UL)]
    [Arguments(true, 0, 0x7FC54321U, 0x7FF8ABCDEF012345UL, 0U, 0UL)]
    [Arguments(true, -1, 0xFF812345U, 0xFFF0123456789ABCUL, 0x80000000U, 0x8000000000000000UL)]
    public async Task 公開経路でf32とf64のselectを呼ぶ_選択したNaNpayloadと符号付き0の生bitを保つ(
        bool typed,
        int condition,
        uint expectedF32NaN,
        ulong expectedF64NaN,
        uint expectedF32Zero,
        ulong expectedF64Zero
    )
    {
        // Arrange
        byte[] f32Select = typed ? [0x1C, 1, 0x7D] : [0x1B];
        byte[] f64Select = typed ? [0x1C, 1, 0x7C] : [0x1B];
        var function = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7D, 0x7D, 0x7C, 0x7C, 0x7F], [0x7D, 0x7C])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [
                                0x20,
                                0,
                                0x20,
                                1,
                                0x20,
                                4,
                                .. f32Select,
                                0x20,
                                2,
                                0x20,
                                3,
                                0x20,
                                4,
                                .. f64Select,
                                0x0B,
                            ]
                        )
                    )
                )
            )
            .Validate()
            .Instantiate([])
            .GetFunction("run");

        // Act
        var nan = function.Invoke([
            WasmValue.FromF32Bits(0xFF812345),
            WasmValue.FromF32Bits(0x7FC54321),
            WasmValue.FromF64Bits(0xFFF0123456789ABC),
            WasmValue.FromF64Bits(0x7FF8ABCDEF012345),
            WasmValue.FromI32(condition),
        ]);
        var zero = function.Invoke([
            WasmValue.FromF32Bits(0x80000000),
            WasmValue.FromF32Bits(0),
            WasmValue.FromF64Bits(0x8000000000000000),
            WasmValue.FromF64Bits(0),
            WasmValue.FromI32(condition),
        ]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(nan.Values[0].AsF32Bits()).IsEqualTo(expectedF32NaN);
            await Assert.That(nan.Values[1].AsF64Bits()).IsEqualTo(expectedF64NaN);
            await Assert.That(zero.Values[0].AsF32Bits()).IsEqualTo(expectedF32Zero);
            await Assert.That(zero.Values[1].AsF64Bits()).IsEqualTo(expectedF64Zero);
        }
    }
}
