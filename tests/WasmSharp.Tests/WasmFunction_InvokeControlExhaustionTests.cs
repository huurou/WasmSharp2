using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ifから直接または相互再帰する_小さい深さ上限で止まり保持状態と新しいlocalsで再実行できる(
        bool mutual
    )
    {
        // Arrange
        var state = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var provider = new WasmHostModule("env");
        provider.Define("state", state);
        byte[] Body(byte next) =>
            [
                // 初期localsを結果として残してから、今回の引数でlocalsを更新する。
                .. Convert.FromHexString("200220032000210220012103230041016A2400"),
                // if内で再帰し、内側の結果を捨てた後に外側のlocalsを返す。
                .. Convert.FromHexString("20000440200041016B200110"),
                next,
                .. Convert.FromHexString("1A1A1A1A0B200220030B"),
            ];
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7F, 0x6F], [0x7F, 0x6F, 0x7F, 0x6F])),
                    HostLinkingModuleBinary.Imports(("env", "state", 3, [0x7F, 1])),
                    HostLinkingModuleBinary.Functions(0, 0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0), ("state", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        ([(1, 0x7F), (1, 0x6F)], Body(mutual ? (byte)1 : (byte)0)),
                        ([(1, 0x7F), (1, 0x6F)], Body(0))
                    )
                )
            )
            .Validate()
            .Instantiate([provider], new(4));
        var function = instance.GetFunction("run");
        var savedReference = new object();
        var failedReference = new object();
        var nextReference = new object();
        var saved = function.Invoke([
            WasmValue.FromI32(0),
            WasmValue.FromExternRef(savedReference),
        ]);

        // Act & Assert
        var exception = await Assert
            .That(() =>
                function.Invoke([WasmValue.FromI32(10), WasmValue.FromExternRef(failedReference)])
            )
            .ThrowsExactly<WasmExhaustionException>();
        var afterFailure = instance.GetGlobal("state").AsI32();
        var next = function.Invoke([WasmValue.FromI32(1), WasmValue.FromExternRef(nextReference)]);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
            await Assert.That(exception.Limit).IsEqualTo(4);
            await Assert
                .That(
                    exception.Message.StartsWith("call stack exhausted", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(exception.Location.FunctionIndex).IsEqualTo(0U);
            await Assert.That(afterFailure).IsEqualTo(5);
            await Assert.That(instance.GetGlobal("state").AsI32()).IsEqualTo(7);
            await Assert.That(instance.GetGlobalResource("state")).IsSameReferenceAs(state);
            await Assert.That(saved.Values[0].AsI32()).IsEqualTo(0);
            await Assert.That(saved.Values[1].AsExternRef()).IsNull();
            await Assert.That(saved.Values[2].AsI32()).IsEqualTo(0);
            await Assert.That(saved.Values[3].AsExternRef()).IsSameReferenceAs(savedReference);
            await Assert.That(next.Values[0].AsI32()).IsEqualTo(0);
            await Assert.That(next.Values[1].AsExternRef()).IsNull();
            await Assert.That(next.Values[2].AsI32()).IsEqualTo(1);
            await Assert.That(next.Values[3].AsExternRef()).IsSameReferenceAs(nextReference);
        }
    }

    [Test]
    public async Task loop内で定義関数とhostを繰り返し呼ぶ_終了済みcallと反復が深さへ蓄積せず参照を保つ()
    {
        // Arrange
        var reference = new object();
        List<(WasmInstance Instance, object? Reference)> observed = [];
        var provider = new WasmHostModule("env");
        provider.Define(
            "host",
            WasmFunction.CreateHost(
                new([WasmValueKind.ExternRef], [WasmValueKind.ExternRef]),
                (instance, values) =>
                {
                    observed.Add((instance, values[0].AsExternRef()));
                    return new(values);
                }
            )
        );
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x6F], [0x6F]), ([0x7F, 0x6F], [0x7F, 0x6F])),
                    HostLinkingModuleBinary.Imports(("env", "host", 0, [0])),
                    HostLinkingModuleBinary.Functions(1, 1),
                    HostLinkingModuleBinary.Exports(("run", 0, 1)),
                    HostLinkingModuleBinary.Code(
                        (
                            [],
                            [
                                // 深さ2で定義関数とhostを順に終了させ、同じloopへ分岐する。
                                .. Convert.FromHexString("03402000200110021A1A200110001A"),
                                .. Convert.FromHexString("200041016B22000D000B200020010B"),
                            ]
                        ),
                        ([], [0x20, 0, 0x20, 1, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([provider], new(2));

        // Act
        var result = instance
            .GetFunction("run")
            .Invoke([WasmValue.FromI32(8), WasmValue.FromExternRef(reference)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsI32()).IsEqualTo(0);
            await Assert.That(result.Values[1].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(observed.Count).IsEqualTo(8);
            foreach (var call in observed)
            {
                await Assert.That(call.Instance).IsSameReferenceAs(instance);
                await Assert.That(call.Reference).IsSameReferenceAs(reference);
            }
        }
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    public async Task 制御内でtrapまたは同期再入またはhost例外に至る_最外の上限と例外実体を保ち次の呼出しを妨げない(
        bool withInstance,
        int failure
    )
    {
        // Arrange
        var state = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var expected = new InvalidOperationException("ホスト由来の例外");
        var fail = true;
        var remaining = 10;
        WasmFunction inner = null!;
        List<WasmInstance> callers = [];
        WasmResults Callback(ReadOnlySpan<WasmValue> values)
        {
            if (fail && failure == 2)
            {
                throw expected;
            }
            if (remaining > 0)
            {
                remaining--;
                inner.Invoke(values);
            }
            return new(values);
        }
        var type = new WasmFunctionType(
            [WasmValueKind.I32, WasmValueKind.ExternRef],
            [WasmValueKind.I32, WasmValueKind.ExternRef]
        );
        var host = withInstance
            ? WasmFunction.CreateHost(
                type,
                (instance, values) =>
                {
                    callers.Add(instance);
                    return Callback(values);
                }
            )
            : WasmFunction.CreateHost(type, Callback);
        var provider = new WasmHostModule("env");
        provider.Define("host", host);
        provider.Define("state", state);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([0x7F, 0x6F], [0x7F, 0x6F]),
                        ([0x7F, 0x6F], [0x7F, 0x6F, 0x7F, 0x6F])
                    ),
                    HostLinkingModuleBinary.Imports(
                        ("env", "host", 0, [0]),
                        ("env", "state", 3, [0x7F, 1])
                    ),
                    HostLinkingModuleBinary.Functions(1),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("state", 3, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, 0x7F), (1, 0x6F)],
                            [
                                .. Convert.FromHexString("200220032000210220012103230041016A2400"),
                                // mode1は数値trap、mode2はhost再入、mode0はそのまま成功する。
                                .. Convert.FromHexString("2000044020004101460440410141006D1A"),
                                .. Convert.FromHexString("052000200110001A1A0B0B"),
                                // 失敗時に飛び越える副作用は、成功した呼出しの数として保持する。
                                .. Convert.FromHexString("2300410A6A2400200220030B"),
                            ]
                        )
                    )
                )
            )
            .Validate();
        var outerInstance = module.Instantiate([provider], new(4));
        var innerInstance = module.Instantiate([provider], new(20));
        var outer = outerInstance.GetFunction("run");
        inner = innerInstance.GetFunction("run");
        var savedReference = new object();
        var failedReference = new object();
        var nextReference = new object();
        var saved = outer.Invoke([WasmValue.FromI32(0), WasmValue.FromExternRef(savedReference)]);

        // Act & Assert
        var actual = await Assert
            .That(() =>
                outer.Invoke([
                    WasmValue.FromI32(failure == 0 ? 1 : 2),
                    WasmValue.FromExternRef(failedReference),
                ])
            )
            .Throws<Exception>();
        var afterFailure = outerInstance.GetGlobal("state").AsI32();
        fail = false;
        remaining = 0;
        var next = outer.Invoke([WasmValue.FromI32(2), WasmValue.FromExternRef(nextReference)]);
        remaining = 5;
        var independent = inner.Invoke([
            WasmValue.FromI32(2),
            WasmValue.FromExternRef(nextReference),
        ]);
        using (Assert.Multiple())
        {
            if (failure == 0)
            {
                await Assert.That(actual).IsTypeOf<WasmTrapException>();
                var trap = (WasmTrapException)actual!;
                await Assert.That(trap.Reason).IsEqualTo(WasmTrapReason.IntegerDivideByZero);
                await Assert.That(trap.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            }
            else if (failure == 1)
            {
                await Assert.That(actual).IsTypeOf<WasmExhaustionException>();
                var exhaustion = (WasmExhaustionException)actual!;
                await Assert.That(exhaustion.Reason).IsEqualTo(WasmExhaustionReason.CallDepthLimit);
                await Assert.That(exhaustion.Limit).IsEqualTo(4);
                await Assert.That(exhaustion.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            }
            else
            {
                await Assert.That(actual).IsSameReferenceAs(expected);
            }
            await Assert.That(afterFailure).IsEqualTo(failure == 1 ? 13 : 12);
            await Assert
                .That(outerInstance.GetGlobal("state").AsI32())
                .IsEqualTo(failure == 1 ? 90 : 89);
            await Assert.That(innerInstance.GetGlobalResource("state")).IsSameReferenceAs(state);
            await Assert.That(saved.Values[0].AsI32()).IsEqualTo(0);
            await Assert.That(saved.Values[1].AsExternRef()).IsNull();
            await Assert.That(saved.Values[2].AsI32()).IsEqualTo(0);
            await Assert.That(saved.Values[3].AsExternRef()).IsSameReferenceAs(savedReference);
            foreach (var result in new[] { next, independent })
            {
                await Assert.That(result.Values[0].AsI32()).IsEqualTo(0);
                await Assert.That(result.Values[1].AsExternRef()).IsNull();
                await Assert.That(result.Values[2].AsI32()).IsEqualTo(2);
                await Assert.That(result.Values[3].AsExternRef()).IsSameReferenceAs(nextReference);
            }
            if (withInstance)
            {
                await Assert.That(callers[0]).IsSameReferenceAs(outerInstance);
                await Assert.That(callers[^1]).IsSameReferenceAs(innerInstance);
            }
            else
            {
                await Assert.That(callers).IsEmpty();
            }
        }
    }
}
