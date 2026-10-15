using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task アクティブデータが重なる_宣言順に書き込み成功した実体を破棄する()
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [new("memory", 0, 20, WasmExternalKind.Memory)],
            100,
            [],
            [],
            [new(new(1, null), 30)],
            [],
            null,
            [ActiveData([11, 22, 33], 1, 50), new([66, 77], null, 55), ActiveData([44, 55], 2, 60)]
        ).Validate();

        // Act
        var instance = module.Instantiate([]);
        byte[] actual = new byte[5];
        instance.GetMemory("memory").Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.SequenceEqual(new byte[] { 0, 11, 44, 55, 0 })).IsTrue();
            await Assert.That(instance.DataSegments.Length).IsEqualTo(3);
            await Assert
                .That(
                    instance
                        .DataSegments.Select(x => x.Length)
                        .SequenceEqual(new uint[] { 0, 2, 0 })
                )
                .IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task パッシブデータを定義する_メモリの有無にかかわらず内容を保持して初期化時には書き込まない(
        bool hasMemory
    )
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            hasMemory ? [new("memory", 0, 20, WasmExternalKind.Memory)] : [],
            100,
            [],
            [],
            hasMemory ? [new(new(1, null), 30)] : [],
            [],
            null,
            [new([11, 22], null, 50)]
        ).Validate();

        // Act
        var instance = module.Instantiate([]);
        var memory = hasMemory ? instance.GetMemory("memory") : new WasmMemory(new(1));
        var initial = new byte[2];
        memory.Read(0, initial);
        var copied = instance.DataSegments[0].TryInitialize(memory, 0, 0, 2);
        var actual = new byte[2];
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.DataSegments.Length).IsEqualTo(1);
            await Assert.That(instance.DataSegments[0].Length).IsEqualTo(2u);
            await Assert.That(instance.Memories.Length).IsEqualTo(hasMemory ? 1 : 0);
            await Assert.That(initial.SequenceEqual(new byte[] { 0, 0 })).IsTrue();
            await Assert.That(copied).IsTrue();
            await Assert.That(actual.SequenceEqual(new byte[] { 11, 22 })).IsTrue();
        }
    }

    [Test]
    public async Task 同じ定義から生成した実体を破棄して再生成する_データの破棄状態と定義メモリが独立する()
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [new("memory", 0, 20, WasmExternalKind.Memory)],
            100,
            [],
            [],
            [new(new(1, null), 30)],
            [],
            null,
            [ActiveData([11], 0, 50), new([22, 33], null, 60)]
        ).Validate();
        var first = module.Instantiate([]);
        first.DataSegments[1].Drop();
        first.GetMemory("memory").Write(0, [99]);

        // Act
        var second = module.Instantiate([]);
        var copied = second.DataSegments[1].TryInitialize(second.GetMemory("memory"), 1, 0, 2);
        var firstBytes = new byte[3];
        first.GetMemory("memory").Read(0, firstBytes);
        var secondBytes = new byte[3];
        second.GetMemory("memory").Read(0, secondBytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(ReferenceEquals(first.DataSegments[0], second.DataSegments[0]))
                .IsFalse();
            await Assert
                .That(ReferenceEquals(first.DataSegments[1], second.DataSegments[1]))
                .IsFalse();
            await Assert.That(first.DataSegments[1].Length).IsEqualTo(0u);
            await Assert.That(second.DataSegments[0].Length).IsEqualTo(0u);
            await Assert.That(second.DataSegments[1].Length).IsEqualTo(2u);
            await Assert.That(copied).IsTrue();
            await Assert.That(firstBytes.SequenceEqual(new byte[] { 99, 0, 0 })).IsTrue();
            await Assert.That(secondBytes.SequenceEqual(new byte[] { 11, 22, 33 })).IsTrue();
            await Assert
                .That(module.DataSegments[0].Bytes.SequenceEqual(new byte[] { 11 }))
                .IsTrue();
            await Assert
                .That(module.DataSegments[1].Bytes.SequenceEqual(new byte[] { 22, 33 }))
                .IsTrue();
        }
    }

    [Test]
    public async Task インポートした不変globalをオフセットに使う_拡張済み共有メモリの現在範囲に書き込む()
    {
        // Arrange
        var memory = new WasmMemory(new(0, 2));
        memory.TryGrow(2, out _);
        var host = new WasmHostModule("env");
        host.Define("memory", memory);
        host.Define("unused", new WasmGlobal(new(WasmValueKind.I64, false), WasmValue.FromI64(0)));
        host.Define(
            "offset",
            new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(65535))
        );
        var module = new WasmModule(
            [],
            [],
            [new("memory", 0, 20, WasmExternalKind.Memory)],
            100,
            [
                new GlobalImport("env", "unused", new(WasmValueKind.I64, false), 21),
                new MemoryImport("env", "memory", new(new(1, 2), 25), 25),
                new GlobalImport("env", "offset", new(WasmValueKind.I32, false), 30),
            ],
            [],
            [],
            [],
            null,
            [
                new(
                    [11, 22, 33],
                    new(
                        0,
                        [
                            DecodedInstruction.WithIndex(new(0, 0x23), 1, 51),
                            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 53),
                        ]
                    ),
                    50
                ),
            ]
        ).Validate();

        // Act
        var instance = module.Instantiate([host]);
        var actual = new byte[5];
        memory.Read(65534, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(instance.GetMemory("memory"), memory)).IsTrue();
            await Assert.That(memory.PageCount).IsEqualTo(2u);
            await Assert.That(actual.SequenceEqual(new byte[] { 0, 11, 22, 33, 0 })).IsTrue();
            await Assert.That(instance.DataSegments[0].Length).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 全アクティブデータの初期化が成功する_Startから初期化済みメモリと破棄済み実体を参照できる(
        bool definedStart
    )
    {
        // Arrange
        WasmInstance? received = null;
        var calls = 0;
        var initial = new byte[3];
        uint[] lengths = [];
        var value = 0;
        var host = new WasmHostModule("env");
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    received = instance;
                    calls++;
                    instance.GetMemory("memory").Read(0, initial);
                    lengths = [.. instance.DataSegments.Select(x => x.Length)];
                    value = instance.GetFunction("read").Invoke([]).Values[0].AsI32();
                    return new([]);
                }
            )
        );
        var module = new WasmModule(
            [new([], []), new([], [WasmValueKind.I32])],
            [
                new(
                    1,
                    70,
                    [],
                    [
                        DecodedInstruction.WithIndex(new(0, 0x23), 0, 71),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 73),
                    ]
                ),
                new(
                    0,
                    80,
                    [],
                    [
                        DecodedInstruction.WithIndex(new(0, 0x10), 0, 81),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 83),
                    ]
                ),
            ],
            [
                new("memory", 0, 35, WasmExternalKind.Memory),
                new("read", 1, 36, WasmExternalKind.Function),
            ],
            100,
            [new FunctionImport("env", "start", 0, 20)],
            [],
            [new(new(1, null), 30)],
            [
                new(
                    new(WasmValueKind.I32, false),
                    [
                        DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(42), 41),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 43),
                    ],
                    40
                ),
            ],
            new(definedStart ? 2u : 0u, 45),
            [ActiveData([11, 22], 0, 50), new([33, 44], null, 55), ActiveData([55], 1, 60)]
        ).Validate();

        // Act
        var result = module.Instantiate([host]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(received, result)).IsTrue();
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(initial.SequenceEqual(new byte[] { 11, 55, 0 })).IsTrue();
            await Assert.That(lengths.SequenceEqual(new uint[] { 0, 2, 0 })).IsTrue();
            await Assert.That(value).IsEqualTo(42);
        }
    }

    [Test]
    [Arguments(65535, false)]
    [Arguments(65537, true)]
    [Arguments(-1, true)]
    [Arguments(-1, false)]
    [Arguments(int.MinValue, false)]
    public async Task アクティブデータが現在メモリの範囲を超える_先行書き込みだけ残して後続とStartを中断する(
        int offset,
        bool empty
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1));
        memory.Write(0, [99, 88, 77]);
        memory.Write(65535, [66]);
        var expected = new byte[65536];
        memory.Read(0, expected);
        expected[0] = 11;
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define("memory", memory);
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
        var module = new WasmModule(
            [new([], [])],
            [],
            [],
            100,
            [
                new MemoryImport("env", "memory", new(new(1, null), 20), 20),
                new FunctionImport("env", "start", 0, 25),
            ],
            [],
            [],
            [],
            new(0, 40),
            [
                ActiveData([11], 0, 50),
                ActiveData(empty ? [] : [22, 33], offset, 60),
                ActiveData([44], 1, 70),
            ]
        ).Validate();
        WasmInstance? returned = null;

        // Act & Assert
        var exception = await Assert
            .That(() => returned = module.Instantiate([host]))
            .ThrowsExactly<WasmTrapException>();
        var actual = new byte[65536];
        memory.Read(0, actual);
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert
                .That(
                    exception.Message.StartsWith(
                        "out of bounds memory access",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Instantiate, 60, null, 11));
            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
            await Assert.That(calls).IsEqualTo(0);
            await Assert.That(returned).IsNull();
        }
    }

    [Test]
    [Arguments(0u, 0)]
    [Arguments(1u, 65536)]
    public async Task 空のアクティブデータの開始位置が現在メモリの末尾にある_初期化に成功してインデックスを保持する(
        uint pages,
        int offset
    )
    {
        // Arrange
        var module = new WasmModule(
            [],
            [],
            [],
            100,
            [],
            [],
            [new(new(pages, null), 30)],
            [],
            null,
            [ActiveData([], offset, 50)]
        ).Validate();

        // Act
        var instance = module.Instantiate([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.Memories[0].ByteLength).IsEqualTo((ulong)offset);
            await Assert.That(instance.DataSegments.Length).IsEqualTo(1);
            await Assert.That(instance.DataSegments[0].Length).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task データ初期化後のStartが失敗する_初期化と変更を保持して保存済み参照を引き続き使える(
        bool hostFailure
    )
    {
        // Arrange
        var expected = new InvalidOperationException("ホスト処理の失敗");
        var memory = new WasmMemory(new(1));
        WasmInstance? saved = null;
        WasmFunction? savedFunction = null;
        WasmMemory? savedMemory = null;
        var host = new WasmHostModule("env");
        host.Define("memory", memory);
        host.Define(
            "save",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    saved = instance;
                    savedFunction = instance.GetFunction("read");
                    savedMemory = instance.GetMemory("memory");
                    savedMemory.Write(2, [33]);
                    if (hostFailure)
                    {
                        throw expected;
                    }
                    return new([]);
                }
            )
        );
        var module = new WasmModule(
            [new([], []), new([], [WasmValueKind.I32])],
            [
                new(
                    1,
                    70,
                    [],
                    [
                        DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(42), 71),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 73),
                    ]
                ),
                new(
                    0,
                    80,
                    [],
                    [
                        DecodedInstruction.WithIndex(new(0, 0x10), 0, 81),
                        DecodedInstruction.WithoutImmediate(new(0, 0x00), 83),
                        DecodedInstruction.WithoutImmediate(new(0, 0x0B), 84),
                    ]
                ),
            ],
            [
                new("memory", 0, 35, WasmExternalKind.Memory),
                new("read", 1, 36, WasmExternalKind.Function),
            ],
            100,
            [
                new MemoryImport("env", "memory", new(new(1, null), 20), 20),
                new FunctionImport("env", "save", 0, 25),
            ],
            [],
            [],
            [],
            new(2, 40),
            [ActiveData([11, 22], 0, 50)]
        ).Validate();

        // Act & Assert
        var exception = await Assert.That(() => module.Instantiate([host])).Throws<Exception>();
        await Assert.That(saved).IsNotNull();

        // Act
        var actual = new byte[3];
        savedMemory!.Read(0, actual);
        saved.GetMemory("memory").Write(0, [44]);
        var value = savedFunction!.Invoke([]).Values[0].AsI32();
        var updated = new byte[1];
        memory.Read(0, updated);

        // Assert
        using (Assert.Multiple())
        {
            if (hostFailure)
            {
                await Assert.That(ReferenceEquals(exception, expected)).IsTrue();
            }
            else
            {
                await Assert.That(exception).IsTypeOf<WasmTrapException>();
                await Assert
                    .That(((WasmTrapException)exception!).Location)
                    .IsEqualTo(new(WasmProcessingStage.Instantiate, 83, 2));
            }
            await Assert.That(ReferenceEquals(savedMemory, memory)).IsTrue();
            await Assert.That(saved.DataSegments[0].Length).IsEqualTo(0u);
            await Assert.That(actual.SequenceEqual(new byte[] { 11, 22, 33 })).IsTrue();
            await Assert.That(updated[0]).IsEqualTo((byte)44);
            await Assert.That(value).IsEqualTo(42);
        }
    }

    [Test]
    public async Task 後続のImportが見つからない_データ初期化を始めず共有メモリを変更しない()
    {
        // Arrange
        var memory = new WasmMemory(new(1));
        memory.Write(0, [99]);
        var host = new WasmHostModule("env");
        host.Define("memory", memory);
        var module = new WasmModule(
            [new([], [])],
            [],
            [],
            100,
            [
                new MemoryImport("env", "memory", new(new(1, null), 20), 20),
                new FunctionImport("env", "missing", 0, 25),
            ],
            [],
            [],
            [],
            new(0, 40),
            [ActiveData([11], 0, 50)]
        ).Validate();

        // Act & Assert
        await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmInstantiateException>();
        var actual = new byte[1];
        memory.Read(0, actual);
        await Assert.That(actual[0]).IsEqualTo((byte)99);
    }

    private static DataDefinition ActiveData(ReadOnlySpan<byte> bytes, int offset, long byteOffset)
    {
        return new(
            bytes,
            new(
                0,
                [
                    DecodedInstruction.WithImmediate(
                        new(0, 0x41),
                        WasmValue.FromI32(offset),
                        byteOffset + 1
                    ),
                    DecodedInstruction.WithoutImmediate(new(0, 0x0B), byteOffset + 3),
                ]
            ),
            byteOffset
        );
    }
}
