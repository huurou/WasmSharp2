using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments("0041000B0100", 0, false)]
    [Arguments("01000100", -1, true)]
    [Arguments("020141000B000100", 1, false)]
    [Arguments("03000100", -1, false)]
    [Arguments("0441000B01D2000B", 0, false)]
    [Arguments("057001D2000B", -1, true)]
    [Arguments("060241000B6F01D06F0B", 2, false)]
    [Arguments("076F01D06F0B", -1, false)]
    public async Task 全flagsの要素を公開経路へ渡す_参照と指定tableと方式ごとの寿命を保つ(
        string segment,
        int activeTable,
        bool passive
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Tables((0x70, 2, null), (0x70, 2, null), (0x6F, 2, null)),
            HostLinkingModuleBinary.Exports(("f", 0, 0), ("t0", 1, 0), ("t1", 1, 1), ("t2", 1, 2)),
            HostLinkingModuleBinary.Section(9, [1, .. Convert.FromHexString(segment)]),
            HostLinkingModuleBinary.Code(([], [0x41, 42, 0x0B]))
        );
        using var stream = new MemoryStream(bytes);
        var module = WasmModule.Decode(stream).Validate();

        // Act
        var instance = module.Instantiate([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.ElementSegments[0].Length).IsEqualTo(passive ? 1u : 0u);
            for (var index = 0; index < 3; index++)
            {
                var value = instance.GetTable("t" + index).Get(0);
                var expected =
                    index == activeTable && index != 2
                        ? WasmValue.FromFuncRef(instance.GetFunction("f"))
                        : (
                            index == 2 ? WasmValue.FromExternRef(null) : WasmValue.FromFuncRef(null)
                        );
                await Assert.That(value).IsEqualTo(expected);
            }
            if (passive)
            {
                var table = instance.GetTable("t0");
                await Assert
                    .That(instance.ElementSegments[0].TryInitialize(table, 1, 0, 1))
                    .IsTrue();
                await Assert
                    .That(table.Get(1).AsFuncRef())
                    .IsSameReferenceAs(instance.GetFunction("f"));
                await Assert.That(instance.ElementSegments[0].Length).IsEqualTo(1u);
                instance.ElementSegments[0].Drop();
                var other = module.Instantiate([]);
                await Assert.That(other.ElementSegments[0].Length).IsEqualTo(1u);
                await Assert
                    .That(table.Get(1).AsFuncRef())
                    .IsSameReferenceAs(instance.GetFunction("f"));
            }
        }
    }

    [Test]
    public async Task Import参照globalと関数参照を要素で使う_完成した関数表と同じ参照を初期化する()
    {
        // Arrange
        var value = WasmValue.FromExternRef(new object());
        var host = new WasmHostModule("env");
        host.Define("g", new WasmGlobal(new(WasmValueKind.ExternRef, false), value));
        host.Define("offset", new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(0)));
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "g", 3, [0x6F, 0]),
                        ("env", "offset", 3, [0x7F, 0])
                    ),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Tables((0x70, 1, null), (0x6F, 1, null)),
                    HostLinkingModuleBinary.Globals((0x70, false, [0xD2, 0, 0x0B])),
                    HostLinkingModuleBinary.Exports(
                        ("f", 0, 0),
                        ("g", 3, 2),
                        ("t0", 1, 0),
                        ("t1", 1, 1)
                    ),
                    HostLinkingModuleBinary.Section(
                        9,
                        [2, 0, 0x23, 1, 0x0B, 1, 0, 6, 1, 0x23, 1, 0x0B, 0x6F, 1, 0x23, 0, 0x0B]
                    ),
                    HostLinkingModuleBinary.Code(([], [0x41, 42, 0x0B]))
                )
            )
            .Validate();

        // Act
        var instance = module.Instantiate([host]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.GetTable("t0").Get(0)).IsEqualTo(instance.GetGlobal("g"));
            await Assert
                .That(instance.GetGlobal("g").AsFuncRef())
                .IsSameReferenceAs(instance.GetFunction("f"));
            await Assert.That(instance.GetTable("t1").Get(0)).IsEqualTo(value);
        }
    }

    [Test]
    [Arguments(1, false)]
    [Arguments(3, true)]
    [Arguments(-1, true)]
    [Arguments(-1, false)]
    [Arguments(int.MinValue, false)]
    public async Task 後続activeの範囲が不正_先行転送だけ残し後続elementとdataとStartを中断する(
        int offset,
        bool empty
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(2));
        var original = WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([])));
        table.Set(0, original);
        table.Set(1, original);
        var memory = new WasmMemory(new(1));
        memory.Write(0, [99]);
        var starts = 0;
        WasmInstance? retained = null;
        var host = new WasmHostModule("env");
        host.Define("table", table);
        host.Define("memory", memory);
        host.Define(
            "observe",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    retained = instance;
                    return new([]);
                }
            )
        );
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    starts++;
                    return new([]);
                }
            )
        );
        byte[] declarative = [3, 0, 1, 2];
        byte[] first = [0, 0x41, 0, 0x0B, 1, 2];
        byte[] failing =
        [
            0,
            0x41,
            .. HostLinkingModuleBinary.Signed(offset),
            0x0B,
            .. (empty ? new byte[] { 0 } : [2, 2, 2]),
        ];
        byte[] elements =
        [
            6,
            .. declarative,
            .. first,
            .. declarative,
            .. failing,
            .. declarative,
            0,
            0x41,
            1,
            0x0B,
            1,
            2,
        ];
        var section = HostLinkingModuleBinary.Section(9, elements);
        var code = HostLinkingModuleBinary.Code(([], [0x10, 0, 0x23, 0, 0x0B]));
        var data = HostLinkingModuleBinary.Section(11, [1, 0, 0x41, 0, 0x0B, 1, 22]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(
                ("env", "table", 1, [0x70, 0, 2]),
                ("env", "memory", 2, [0, 1]),
                ("env", "observe", 0, [0]),
                ("env", "start", 0, [0])
            ),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 42, 0x0B])),
            HostLinkingModuleBinary.Exports(("f", 0, 2), ("table", 1, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Start(1),
            section,
            code,
            data
        );
        var module = WasmModule.Decode(bytes).Validate();
        var failureOffset =
            bytes.Length
            - code.Length
            - data.Length
            - elements.Length
            + 1
            + declarative.Length * 2
            + first.Length;

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmTrapException>();
        var stored = table.Get(0).AsFuncRef()!;
        var result = stored.Invoke([]).Values[0].AsI32();
        byte[] actual = new byte[1];
        memory.Read(0, actual);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.TableOutOfBounds);
            await Assert
                .That(
                    exception.Message.StartsWith(
                        "out of bounds table access",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Instantiate, failureOffset, null, 9));
            await Assert.That(table.Get(1)).IsEqualTo(original);
            await Assert.That(actual[0]).IsEqualTo((byte)99);
            await Assert.That(starts).IsEqualTo(0);
            await Assert.That(result).IsEqualTo(42);
            await Assert.That(retained).IsNotNull();
            await Assert.That(retained!.GetFunction("f")).IsSameReferenceAs(stored);
            await Assert
                .That(
                    retained
                        .ElementSegments.Select(x => x.Length)
                        .SequenceEqual(new uint[] { 0, 0, 0, empty ? 0u : 2u, 1, 1 })
                )
                .IsTrue();
            await Assert.That(retained.DataSegments[0].Length).IsEqualTo(1u);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 要素処理後にdataが初期化される_成功時のStartと失敗後の関数が完成instanceを参照する(
        bool invalidData
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var memory = new WasmMemory(new(1));
        memory.Write(0, [99]);
        WasmInstance? observed = null;
        var atStart = new byte[1];
        uint[] elementsAtStart = [];
        uint dataAtStart = 1;
        WasmFunction? functionAtStart = null;
        var starts = 0;
        var host = new WasmHostModule("env");
        host.Define("table", table);
        host.Define("memory", memory);
        host.Define(
            "observe",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    observed = instance;
                    return new([]);
                }
            )
        );
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    starts++;
                    observed = instance;
                    instance.GetMemory("memory").Read(0, atStart);
                    elementsAtStart = [.. instance.ElementSegments.Select(x => x.Length)];
                    dataAtStart = instance.DataSegments[0].Length;
                    functionAtStart = instance.GetTable("table").Get(0).AsFuncRef();
                    return new([]);
                }
            )
        );
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "table", 1, [0x70, 0, 1]),
                        ("env", "memory", 2, [0, 1]),
                        ("env", "observe", 0, [0]),
                        ("env", "start", 0, [0])
                    ),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Exports(("f", 0, 2), ("table", 1, 0), ("memory", 2, 0)),
                    HostLinkingModuleBinary.Start(1),
                    HostLinkingModuleBinary.Section(
                        9,
                        [3, 0, 0x41, 0, 0x0B, 1, 2, 1, 0, 1, 2, 3, 0, 1, 2]
                    ),
                    HostLinkingModuleBinary.Code(([], [0x10, 0, 0x0B])),
                    HostLinkingModuleBinary.Section(
                        11,
                        [
                            1,
                            0,
                            0x41,
                            .. HostLinkingModuleBinary.Signed(invalidData ? 65536 : 0),
                            0x0B,
                            1,
                            22,
                        ]
                    )
                )
            )
            .Validate();

        if (invalidData)
        {
            // Act & Assert
            var exception = await Assert
                .That(() => module.Instantiate([host]))
                .ThrowsExactly<WasmTrapException>();
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert.That(starts).IsEqualTo(0);
            table.Get(0).AsFuncRef()!.Invoke([]);
        }
        else
        {
            // Act
            var instance = module.Instantiate([host]);

            // Assert
            await Assert.That(observed).IsSameReferenceAs(instance);
            await Assert.That(starts).IsEqualTo(1);
            await Assert.That(atStart[0]).IsEqualTo((byte)22);
            await Assert.That(elementsAtStart.SequenceEqual(new uint[] { 0, 1, 0 })).IsTrue();
            await Assert.That(dataAtStart).IsEqualTo(0u);
            await Assert.That(functionAtStart).IsSameReferenceAs(instance.GetFunction("f"));
        }

        // Assert
        byte[] actual = new byte[1];
        memory.Read(0, actual);
        using (Assert.Multiple())
        {
            await Assert.That(actual[0]).IsEqualTo(invalidData ? (byte)99 : (byte)22);
            await Assert.That(observed!.GetTable("table")).IsSameReferenceAs(table);
            await Assert
                .That(table.Get(0).AsFuncRef())
                .IsSameReferenceAs(observed.GetFunction("f"));
            await Assert
                .That(
                    observed
                        .ElementSegments.Select(x => x.Length)
                        .SequenceEqual(new uint[] { 0, 1, 0 })
                )
                .IsTrue();
            await Assert.That(observed.DataSegments[0].Length).IsEqualTo(invalidData ? 1u : 0u);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task 空activeの開始位置が末尾にある_初期化に成功する(int count)
    {
        // Arrange
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Tables((0x70, (uint)count, null)),
                    HostLinkingModuleBinary.Section(9, [1, 0, 0x41, (byte)count, 0x0B, 0])
                )
            )
            .Validate();

        // Act
        var instance = module.Instantiate([]);

        // Assert
        await Assert.That(instance.ElementSegments[0].Length).IsEqualTo(0u);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 後続importの解決か型照合が成立しない_先行共有tableとメモリを変更せずStartを呼ばない(
        bool wrongType
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(1));
        var original = WasmValue.FromFuncRef(WasmFunction.CreateHost(new([], []), _ => new([])));
        table.Set(0, original);
        var memory = new WasmMemory(new(1));
        memory.Write(0, [99]);
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define("table", table);
        host.Define("memory", memory);
        if (wrongType)
        {
            host.Define(
                "missing",
                new WasmGlobal(new(WasmValueKind.I64, false), WasmValue.FromI64(0))
            );
        }
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "table", 1, [0x70, 0, 1]),
                        ("env", "memory", 2, [0, 1]),
                        ("env", "missing", 3, [0x7F, 0]),
                        ("env", "start", 0, [0])
                    ),
                    HostLinkingModuleBinary.Start(0),
                    HostLinkingModuleBinary.Section(9, [1, 4, 0x41, 0, 0x0B, 1, 0xD0, 0x70, 0x0B]),
                    HostLinkingModuleBinary.Section(11, [1, 0, 0x41, 0, 0x0B, 1, 22])
                )
            )
            .Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmInstantiateException>();
        byte[] actual = new byte[1];
        memory.Read(0, actual);
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(
                    wrongType
                        ? WasmInstantiateReason.TypeMismatch
                        : WasmInstantiateReason.MissingImport
                );
            await Assert.That(table.Get(0)).IsEqualTo(original);
            await Assert.That(actual[0]).IsEqualTo((byte)99);
            await Assert.That(calls).IsEqualTo(0);
        }
    }
}
