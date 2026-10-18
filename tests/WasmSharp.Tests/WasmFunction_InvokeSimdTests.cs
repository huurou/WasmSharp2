using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(
        "0C",
        "0123456789ABCDEFFEDCBA9876543210",
        0xEFCDAB8967452301UL,
        0x1032547698BADCFEUL
    )]
    [Arguments(
        "8C00",
        "4523C17F0000000000000080230180FF",
        0x000000007FC12345UL,
        0xFF80012380000000UL
    )]
    [Arguments(
        "8C80808000",
        "BC9A78563412F87F0000000000000080",
        0x7FF8123456789ABCUL,
        0x8000000000000000UL
    )]
    public async Task V128定数の最短か許容非最短番号がある_両入力の4段階で全bitを保持する(
        string opcode,
        string bits,
        ulong low,
        ulong high
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString("FD" + opcode + bits + "0B")))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        Array.Clear(bytes);
        foreach (var module in modules)
        {
            module.Validate();
            var result = module.Instantiate([]).GetFunction("run").Invoke([]);

            // Assert
            using (Assert.Multiple())
            {
                await Assert
                    .That(module.Functions[0].Instructions[0].Immediate.AsV128())
                    .IsEqualTo((low, high));
                await Assert.That(result.Values.Length).IsEqualTo(1);
                await Assert.That(result.Values[0].AsV128()).IsEqualTo((low, high));
            }
        }
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, -1)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, int.MinValue)]
    public async Task V128のselectがある_I32条件で選択したNaNと符号付きゼロの全bitを保持する(
        bool typed,
        int condition
    )
    {
        // Arrange
        byte[] select = typed ? [0x1C, 1, 0x7B] : [0x1B];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7B, 0x7B, 0x7F], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. select, 0x0B]))
        );
        var first = WasmValue.FromV128(0x800000007FC12345UL, 0xFF80012300000000UL);
        var second = WasmValue.FromV128(0xFFF8123456789ABCUL, 0x8000000000000000UL);
        var module = WasmModule.Decode(bytes);

        // Act
        var result = module
            .Validate()
            .Instantiate([])
            .GetFunction("run")
            .Invoke([first, second, WasmValue.FromI32(condition)]);

        // Assert
        await Assert
            .That(result.Values[0].AsV128())
            .IsEqualTo((condition != 0 ? first : second).AsV128());
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task V128の複数値をloopで交換して分岐する_Dropした値を残さず全bitと結果順序を保つ(
        int iterations
    )
    {
        // Arrange
        var first = WasmValue.FromV128(0x800000007FC12345, 0x0123456789ABCDEF);
        var second = WasmValue.FromV128(0xFEDCBA9876543210, 0xFFF8123456789ABC);
        var function = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([0x7B, 0x7B, 0x7F], [0x7B, 0x7B]),
                        ([0x7B, 0x7B], [0x7B, 0x7B])
                    ),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        (
                            [(1, 0x7F), (2, 0x7B)],
                            [
                                0x20,
                                2,
                                0x21,
                                3,
                                0x20,
                                0,
                                0x20,
                                1,
                                // loopの2引数をlocalsへ移し、交換した値を次の反復へ渡す
                                0x03,
                                1,
                                0x21,
                                5,
                                0x21,
                                4,
                                0x20,
                                4,
                                0x1A,
                                0x20,
                                5,
                                0x20,
                                4,
                                0x20,
                                3,
                                0x41,
                                1,
                                0x6B,
                                0x22,
                                3,
                                0x0D,
                                0,
                                0x0B,
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
        var result = function.Invoke([first, second, WasmValue.FromI32(iterations)]);

        // Assert
        await Assert
            .That(result.Values.SequenceEqual(iterations == 1 ? [second, first] : [first, second]))
            .IsTrue();
    }

    [Test]
    public async Task V128を別moduleの同じ構造の型で間接callする_複数結果と所属instanceを保つ()
    {
        // Arrange
        var callee = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7B, 0x7B], [0x7B, 0x7B, 0x7F])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 42, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x20, 1, 0x20, 0, 0x23, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([]);
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        table.Set(0, WasmValue.FromFuncRef(callee.GetFunction("run")));
        var provider = new WasmHostModule("env");
        provider.Define("table", table);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([], []),
                        ([0x7B, 0x7B], [0x7B, 0x7B, 0x7F]),
                        ([0x7B, 0x7B, 0x7F], [0x7B, 0x7B, 0x7F])
                    ),
                    HostLinkingModuleBinary.Imports(("env", "table", 1, [0x70, 0, 1])),
                    HostLinkingModuleBinary.Functions(2),
                    HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 9, 0x0B])),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(
                        ([], [0x20, 0, 0x20, 1, 0x20, 2, 0x11, 1, 0, 0x0B])
                    )
                )
            )
            .Validate();
        var first = WasmValue.FromV128(0x800000007FC12345, 0x0123456789ABCDEF);
        var second = WasmValue.FromV128(0xFEDCBA9876543210, 0xFFF8123456789ABC);

        // Act
        var result = module
            .Instantiate([provider])
            .GetFunction("run")
            .Invoke([first, second, WasmValue.FromI32(0)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(result.Values.SequenceEqual([second, first, WasmValue.FromI32(42)]))
                .IsTrue();
            await Assert.That(module.Types[1]).IsNotSameReferenceAs(callee.GetFunction("run").Type);
            await Assert
                .That(table.Get(0).AsFuncRef())
                .IsSameReferenceAs(callee.GetFunction("run"));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task V128の間接callで引数か結果の型が不一致_Callback前にtrapとなり次の呼び出しで回復する(
        bool mismatchedResult
    )
    {
        // Arrange
        var calls = 0;
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        table.Set(
            0,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    mismatchedResult
                        ? new([WasmValueKind.V128], [WasmValueKind.I64])
                        : new([WasmValueKind.I64], [WasmValueKind.V128]),
                    _ =>
                    {
                        calls++;
                        return new([]);
                    }
                )
            )
        );
        var provider = new WasmHostModule("env");
        provider.Define("table", table);
        var instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7B], [0x7B])),
                    HostLinkingModuleBinary.Imports(("env", "table", 1, [0x70, 0, 1])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x20, 0, 0x41, 0, 0x11, 0, 0, 0x0B]))
                )
            )
            .Validate()
            .Instantiate([provider], new(2));
        var value = WasmValue.FromV128(0x800000007FC12345, 0xFFF8123456789ABC);
        var function = instance.GetFunction("run");
        WasmInstance? observed = null;

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke([value]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.IndirectCallTypeMismatch);
            await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Invoke);
            await Assert.That(calls).IsEqualTo(0);
        }

        // Act
        table.Set(
            0,
            WasmValue.FromFuncRef(
                WasmFunction.CreateHost(
                    new([WasmValueKind.V128], [WasmValueKind.V128]),
                    (caller, values) =>
                    {
                        observed = caller;
                        calls++;
                        return new(values);
                    }
                )
            )
        );
        var first = function.Invoke([value]);
        var second = function.Invoke([WasmValue.FromV128(ulong.MaxValue, 0)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values.SequenceEqual([value])).IsTrue();
            await Assert.That(second.Values[0].AsV128()).IsEqualTo((ulong.MaxValue, 0UL));
            await Assert.That(observed).IsSameReferenceAs(instance);
            await Assert.That(calls).IsEqualTo(2);
        }
    }

    [Test]
    public async Task V128を定義関数と再exportしたhostへ渡す_関数の同一性と直前instanceと明示型を保つ()
    {
        // Arrange
        List<WasmInstance> observed = [];
        var host = WasmFunction.CreateHost(
            new([WasmValueKind.V128], [WasmValueKind.V128]),
            (caller, values) =>
            {
                observed.Add(caller);
                return new(values);
            }
        );
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7B], [0x7B])),
                    HostLinkingModuleBinary.Imports(("env", "host", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("run", 0, 1), ("host", 0, 0)),
                    HostLinkingModuleBinary.Code(([], [0x20, 0, 0x10, 0, 0x0B]))
                )
            )
            .Validate();
        WasmInstance Instantiate(WasmFunction function)
        {
            var provider = new WasmHostModule("env");
            provider.Define("host", function);
            return module.Instantiate([provider]);
        }
        var source = Instantiate(host);
        var throughDefinition = Instantiate(source.GetFunction("run"));
        var throughReexport = Instantiate(source.GetFunction("host"));
        var firstValue = WasmValue.FromV128(0x800000007FC12345, 0x0123456789ABCDEF);
        var secondValue = WasmValue.FromV128(0xFEDCBA9876543210, 0xFFF8123456789ABC);

        // Act & Assert
        await Assert
            .That(() => throughReexport.GetFunction("run").Invoke([WasmValue.FromI64(0)]))
            .ThrowsExactly<ArgumentException>();

        // Act
        var first = throughDefinition.GetFunction("run").Invoke([firstValue]);
        var second = throughReexport.GetFunction("run").Invoke([secondValue]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values.SequenceEqual([firstValue])).IsTrue();
            await Assert.That(second.Values.SequenceEqual([secondValue])).IsTrue();
            await Assert.That(observed.Count).IsEqualTo(2);
            await Assert.That(observed[0]).IsSameReferenceAs(source);
            await Assert.That(observed[1]).IsSameReferenceAs(throughReexport);
            await Assert.That(source.GetFunction("host")).IsSameReferenceAs(host);
            await Assert.That(throughReexport.GetFunction("host")).IsSameReferenceAs(host);
            await Assert
                .That(throughDefinition.GetFunction("host"))
                .IsSameReferenceAs(source.GetFunction("run"));
            await Assert.That(host.Type.Parameters.SequenceEqual([WasmValueKind.V128])).IsTrue();
            await Assert.That(host.Type.Results.SequenceEqual([WasmValueKind.V128])).IsTrue();
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task V128のhost同期再入が成功または失敗する_外側値とlocalsと上限を復元し返却済み結果を保つ(
        bool withInstance,
        bool hostThrows
    )
    {
        // Arrange
        WasmInstance instance = null!;
        WasmValue[] values =
        [
            WasmValue.FromV128(0x800000007FC12345, 0x0123456789ABCDEF),
            WasmValue.FromV128(0xFEDCBA9876543210, 0xFFF8123456789ABC),
        ];
        List<WasmResults> recovered = [];
        List<WasmResults> inner = [];
        List<Exception?> caught = [];
        List<WasmInstance> observed = [];
        var expected = new InvalidOperationException("nested v128 host failure");
        var calls = 0;
        WasmResults Callback(ReadOnlySpan<WasmValue> arguments)
        {
            var nestedValue = values[1 - calls++];
            Exception? actual = null;
            try
            {
                var target = hostThrows ? "nested" : "echo";
                inner.Add(instance.GetFunction(target).Invoke([nestedValue]));
            }
            catch (Exception exception)
            {
                actual = exception;
            }
            caught.Add(actual);
            // 同じcallback内で再入し直し、内側の失敗後に深さと値が復元されたことを確認する
            recovered.Add(instance.GetFunction("echo").Invoke([nestedValue]));
            return new(arguments);
        }
        var type = new WasmFunctionType([WasmValueKind.V128], [WasmValueKind.V128]);
        var provider = new WasmHostModule("env");
        provider.Define(
            "host",
            withInstance
                ? WasmFunction.CreateHost(
                    type,
                    (caller, arguments) =>
                    {
                        observed.Add(caller);
                        return Callback(arguments);
                    }
                )
                : WasmFunction.CreateHost(type, Callback)
        );
        provider.Define("nested", WasmFunction.CreateHost(type, _ => throw expected));
        instance = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([0x7B], [0x7B]), ([0x7B], [0x7B, 0x7B, 0x7B])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "host", 0, [0]),
                        ("env", "nested", 0, [0])
                    ),
                    HostLinkingModuleBinary.Functions(1, 0, 0),
                    HostLinkingModuleBinary.Exports(
                        ("run", 0, 2),
                        ("echo", 0, 3),
                        ("nested", 0, 4)
                    ),
                    HostLinkingModuleBinary.Code(
                        ([(1, 0x7B)], [0x20, 0, 0x21, 1, 0x20, 0, 0x20, 0, 0x10, 0, 0x20, 1, 0x0B]),
                        ([(2048, 0x7B)], [0x20, 0, 0x0B]),
                        ([], [0x20, 0, 0x10, 1, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([provider], new(4));

        // Act
        var first = instance.GetFunction("run").Invoke([values[0]]);
        var second = instance.GetFunction("run").Invoke([values[1]]);
        var after = instance.GetFunction("echo").Invoke([values[0]]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(calls).IsEqualTo(2);
            await Assert
                .That(first.Values.SequenceEqual([values[0], values[0], values[0]]))
                .IsTrue();
            await Assert
                .That(second.Values.SequenceEqual([values[1], values[1], values[1]]))
                .IsTrue();
            await Assert.That(after.Values.SequenceEqual([values[0]])).IsTrue();
            await Assert.That(recovered[0].Values.SequenceEqual([values[1]])).IsTrue();
            await Assert.That(recovered[1].Values.SequenceEqual([values[0]])).IsTrue();
            await Assert.That(observed.Count).IsEqualTo(withInstance ? 2 : 0);
            foreach (var caller in observed)
            {
                await Assert.That(caller).IsSameReferenceAs(instance);
            }
            await Assert.That(inner.Count).IsEqualTo(hostThrows ? 0 : 2);
            if (!hostThrows)
            {
                await Assert.That(inner[0].Values.SequenceEqual([values[1]])).IsTrue();
                await Assert.That(inner[1].Values.SequenceEqual([values[0]])).IsTrue();
            }
            foreach (var exception in caught)
            {
                await Assert
                    .That(exception?.GetType())
                    .IsEqualTo(hostThrows ? typeof(InvalidOperationException) : null);
                if (hostThrows)
                {
                    await Assert.That(exception).IsSameReferenceAs(expected);
                }
            }
        }
    }
}
