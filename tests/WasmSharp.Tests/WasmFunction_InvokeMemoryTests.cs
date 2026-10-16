using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(false, 0u, 1)]
    [Arguments(true, 0u, 1)]
    [Arguments(false, 1u, 2)]
    [Arguments(true, 1u, 2)]
    [Arguments(false, 3u, 0)]
    [Arguments(true, 3u, 0)]
    public async Task メモリサイズ取得と拡張を公開4段階で呼び出す_現在サイズと拡張前サイズを返す(
        bool imported,
        uint initialPages,
        int delta
    )
    {
        // Arrange
        var hostMemory = new WasmMemory(new(initialPages, 3));
        var host = new WasmHostModule("env");
        host.Define("m", hostMemory);
        var instance = CreateMemorySizeAndGrowModule(imported, initialPages, 3)
            .Instantiate(imported ? [host] : []);
        var memory = instance.GetMemory("memory");
        var original = Enumerable
            .Range(0, (int)initialPages * 65536)
            .Select(x => (byte)(x % 251 + 1))
            .ToArray();
        memory.Write(0, original);
        var expectedPages = initialPages + (uint)delta;
        var actual = new byte[expectedPages * 65536];

        // Act
        var before = instance.GetFunction("size").Invoke([]);
        var previous = instance.GetFunction("grow").Invoke([WasmValue.FromI32(delta)]);
        var after = instance.GetFunction("size").Invoke([]);
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before.Values[0].AsI32()).IsEqualTo((int)initialPages);
            await Assert.That(previous.Values[0].AsI32()).IsEqualTo((int)initialPages);
            await Assert.That(after.Values[0].AsI32()).IsEqualTo((int)expectedPages);
            await Assert.That(memory.PageCount).IsEqualTo(expectedPages);
            await Assert.That(actual.AsSpan(0, original.Length).SequenceEqual(original)).IsTrue();
            await Assert.That(actual[original.Length..].All(x => x == 0)).IsTrue();
            if (imported)
            {
                await Assert.That(memory).IsSameReferenceAs(hostMemory);
                await Assert.That(hostMemory.PageCount).IsEqualTo(expectedPages);
            }
        }
    }

    [Test]
    [Arguments(3u, 3)]
    [Arguments(null, 65536)]
    [Arguments(null, -1)]
    [Arguments(null, int.MinValue)]
    public async Task 符号なし拡張量が実体の最大値かCore上限を超える_失敗値を返しサイズと内容を保持する(
        uint? maximumPages,
        int delta
    )
    {
        // Arrange
        foreach (var imported in new[] { false, true })
        {
            var hostMemory = new WasmMemory(new(1, maximumPages));
            var host = new WasmHostModule("env");
            host.Define("m", hostMemory);
            var instance = CreateMemorySizeAndGrowModule(imported, 1, maximumPages)
                .Instantiate(imported ? [host] : []);
            var memory = instance.GetMemory("memory");
            var original = Enumerable.Range(0, 65536).Select(x => (byte)(x % 251 + 1)).ToArray();
            memory.Write(0, original);
            var actual = new byte[original.Length];

            // Act
            var result = instance.GetFunction("grow").Invoke([WasmValue.FromI32(delta)]);
            var size = instance.GetFunction("size").Invoke([]);
            memory.Read(0, actual);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(result.Values[0].AsI32()).IsEqualTo(-1);
                await Assert.That(size.Values[0].AsI32()).IsEqualTo(1);
                await Assert.That(memory.PageCount).IsEqualTo(1u);
                await Assert.That(memory.ByteLength).IsEqualTo(65536UL);
                await Assert.That(actual.SequenceEqual(original)).IsTrue();
            }
        }
    }

    [Test]
    public async Task ホストと複数instanceが同じメモリを拡張して書き込む_後続命令から更新後のサイズと内容を観測する()
    {
        // Arrange
        var memory = new WasmMemory(new(1, 4));
        var host = new WasmHostModule("env");
        host.Define("m", memory);
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(
                        ([], [0x7F]),
                        ([0x7F], [0x7F]),
                        ([0x7F, 0x7F], [])
                    ),
                    HostLinkingModuleBinary.Imports(
                        ("env", "m", 2, HostLinkingModuleBinary.Limits(1, 6))
                    ),
                    HostLinkingModuleBinary.Functions(0, 1, 1, 2),
                    HostLinkingModuleBinary.Exports(
                        ("size", 0, 0),
                        ("grow", 0, 1),
                        ("load", 0, 2),
                        ("store", 0, 3),
                        ("memory", 2, 0)
                    ),
                    HostLinkingModuleBinary.Code(
                        ([], [0x3F, 0, 0x0B]),
                        ([], [0x20, 0, 0x40, 0, 0x0B]),
                        ([], [0x20, 0, 0x2D, 0, 0, 0x0B]),
                        ([], [0x20, 0, 0x20, 1, 0x3A, 0, 0, 0x0B])
                    )
                )
            )
            .Validate();
        var first = module.Instantiate([host]);
        var second = module.Instantiate([host]);

        // Act
        var guestPrevious = first.GetFunction("grow").Invoke([WasmValue.FromI32(1)]);
        var secondSize = second.GetFunction("size").Invoke([]);
        var hostSuccess = memory.TryGrow(1, out var hostPrevious);
        memory.Write(131072, [0xAB]);
        var hostByte = second.GetFunction("load").Invoke([WasmValue.FromI32(131072)]);
        first.GetFunction("store").Invoke([WasmValue.FromI32(131073), WasmValue.FromI32(0xCD)]);
        var guestByte = second.GetFunction("load").Invoke([WasmValue.FromI32(131073)]);
        var finalSize = first.GetFunction("size").Invoke([]);
        var actual = new byte[2];
        memory.Read(131072, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.GetMemory("memory")).IsSameReferenceAs(memory);
            await Assert.That(second.GetMemory("memory")).IsSameReferenceAs(memory);
            await Assert.That(guestPrevious.Values[0].AsI32()).IsEqualTo(1);
            await Assert.That(secondSize.Values[0].AsI32()).IsEqualTo(2);
            await Assert.That(hostSuccess).IsTrue();
            await Assert.That(hostPrevious).IsEqualTo(2u);
            await Assert.That(memory.PageCount).IsEqualTo(3u);
            await Assert.That(finalSize.Values[0].AsI32()).IsEqualTo(3);
            await Assert.That(hostByte.Values[0].AsI32()).IsEqualTo(0xAB);
            await Assert.That(guestByte.Values[0].AsI32()).IsEqualTo(0xCD);
            await Assert.That(Convert.ToHexString(actual)).IsEqualTo("ABCD");
        }
    }

    [Test]
    [Arguments((byte)0x28, (byte)0x7F, (byte)2, "81828384", 0x84838281UL)]
    [Arguments((byte)0x29, (byte)0x7E, (byte)3, "8182838485868788", 0x8887868584838281UL)]
    [Arguments((byte)0x2A, (byte)0x7D, (byte)2, "4523C1FF", 0xFFC12345UL)]
    [Arguments((byte)0x2B, (byte)0x7C, (byte)3, "BC9A78563412F8FF", 0xFFF8123456789ABCUL)]
    [Arguments((byte)0x2C, (byte)0x7F, (byte)0, "81", 0xFFFFFF81UL)]
    [Arguments((byte)0x2D, (byte)0x7F, (byte)0, "81", 0x81UL)]
    [Arguments((byte)0x2E, (byte)0x7F, (byte)1, "8182", 0xFFFF8281UL)]
    [Arguments((byte)0x2F, (byte)0x7F, (byte)1, "8182", 0x8281UL)]
    [Arguments((byte)0x30, (byte)0x7E, (byte)0, "81", 0xFFFFFFFFFFFFFF81UL)]
    [Arguments((byte)0x31, (byte)0x7E, (byte)0, "81", 0x81UL)]
    [Arguments((byte)0x32, (byte)0x7E, (byte)1, "8182", 0xFFFFFFFFFFFF8281UL)]
    [Arguments((byte)0x33, (byte)0x7E, (byte)1, "8182", 0x8281UL)]
    [Arguments((byte)0x34, (byte)0x7E, (byte)2, "81828384", 0xFFFFFFFF84838281UL)]
    [Arguments((byte)0x35, (byte)0x7E, (byte)2, "81828384", 0x84838281UL)]
    public async Task ロード命令を公開4段階で呼び出す_定義とインポートのメモリから型と値を返す(
        byte opcode,
        byte valueType,
        byte alignment,
        string initial,
        ulong expectedBits
    )
    {
        // Arrange
        foreach (var imported in new[] { false, true })
        {
            var bytes = HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F], [valueType])),
                imported ? HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])) : [],
                HostLinkingModuleBinary.Functions(0),
                imported ? [] : HostLinkingModuleBinary.Memories((1, null)),
                HostLinkingModuleBinary.Exports(("run", 0, 0), ("memory", 2, 0)),
                HostLinkingModuleBinary.Code(([], [0x20, 0, opcode, alignment, 0x82, 0, 0x0B]))
            );
            var hostMemory = new WasmMemory(new(1, null));
            var host = new WasmHostModule("env");
            host.Define("m", hostMemory);
            var expected = MemoryValue(valueType, expectedBits);

            // Act
            var module = WasmModule.Decode(bytes);
            Array.Fill(bytes, (byte)0xFF);
            var instance = module.Validate().Instantiate(imported ? [host] : []);
            var memory = instance.GetMemory("memory");
            var initialBytes = Convert.FromHexString(initial);
            memory.Write(3, initialBytes);
            memory.Write((ulong)(3 + initialBytes.Length), [0xEE]);
            var result = instance.GetFunction("run").Invoke([WasmValue.FromI32(1)]);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(result.Values.Length).IsEqualTo(1);
                await Assert.That(result.Values[0]).IsEqualTo(expected);
                if (imported)
                {
                    await Assert.That(memory).IsSameReferenceAs(hostMemory);
                }
            }
        }
    }

    [Test]
    [Arguments((byte)0x36, (byte)0x7F, (byte)2, 0x87654321UL, "21436587")]
    [Arguments((byte)0x37, (byte)0x7E, (byte)3, 0xFEDCBA9876543210UL, "1032547698BADCFE")]
    [Arguments((byte)0x38, (byte)0x7D, (byte)2, 0xFFC12345UL, "4523C1FF")]
    [Arguments((byte)0x39, (byte)0x7C, (byte)3, 0xFFF8123456789ABCUL, "BC9A78563412F8FF")]
    [Arguments((byte)0x3A, (byte)0x7F, (byte)0, 0x87654321UL, "21")]
    [Arguments((byte)0x3B, (byte)0x7F, (byte)1, 0x87654321UL, "2143")]
    [Arguments((byte)0x3C, (byte)0x7E, (byte)0, 0xFEDCBA9876543210UL, "10")]
    [Arguments((byte)0x3D, (byte)0x7E, (byte)1, 0xFEDCBA9876543210UL, "1032")]
    [Arguments((byte)0x3E, (byte)0x7E, (byte)2, 0xFEDCBA9876543210UL, "10325476")]
    public async Task ストア命令を公開4段階で呼び出す_定義とインポートのメモリへ指定幅だけ書き込む(
        byte opcode,
        byte valueType,
        byte alignment,
        ulong bits,
        string expected
    )
    {
        // Arrange
        foreach (var imported in new[] { false, true })
        {
            var bytes = HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F, valueType], [])),
                imported ? HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])) : [],
                HostLinkingModuleBinary.Functions(0),
                imported ? [] : HostLinkingModuleBinary.Memories((1, null)),
                HostLinkingModuleBinary.Exports(("run", 0, 0), ("memory", 2, 0)),
                HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, opcode, alignment, 2, 0x0B]))
            );
            var memory = new WasmMemory(new(1, null));
            var host = new WasmHostModule("env");
            host.Define("m", memory);
            var instance = WasmModule.Decode(bytes).Validate().Instantiate(imported ? [host] : []);
            var exported = instance.GetMemory("memory");
            exported.Write(2, Enumerable.Repeat((byte)0xCC, 10).ToArray());

            // Act
            var result = instance
                .GetFunction("run")
                .Invoke([WasmValue.FromI32(1), MemoryValue(valueType, bits)]);
            var actual = new byte[expected.Length / 2 + 2];
            exported.Read(2, actual);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(result.Values.IsEmpty).IsTrue();
                await Assert.That(Convert.ToHexString(actual)).IsEqualTo("CC" + expected + "CC");
                if (imported)
                {
                    await Assert.That(exported).IsSameReferenceAs(memory);
                }
            }
        }
    }

    [Test]
    [Arguments(false, -1, 0u)]
    [Arguments(false, 1, uint.MaxValue)]
    [Arguments(true, 65535, 0u)]
    public async Task メモリ命令の実効範囲が領域を超える_Invokeの元命令位置でtrapし内容を保持する(
        bool store,
        int address,
        uint offset
    )
    {
        // Arrange
        var opcode = store ? (byte)0x36 : (byte)0x28;
        var encodedOffset = HostLinkingModuleBinary.Unsigned(offset);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], store ? [] : [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("run", 0, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    [
                        0x20,
                        0,
                        .. store ? new byte[] { 0x41, 1 } : [],
                        opcode,
                        0,
                        .. encodedOffset,
                        0x0B,
                    ]
                )
            )
        );
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([]);
        var memory = instance.GetMemory("memory");
        memory.Write(65532, [0x81, 0x82, 0x83, 0x84]);

        // Act & Assert
        var exception = await Assert
            .That(() => instance.GetFunction("run").Invoke([WasmValue.FromI32(address)]))
            .ThrowsExactly<WasmTrapException>();
        var actual = new byte[4];
        memory.Read(65532, actual);
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
                .IsEqualTo(
                    new(WasmProcessingStage.Invoke, bytes.Length - encodedOffset.Length - 3, 0)
                );
            await Assert.That(Convert.ToHexString(actual)).IsEqualTo("81828384");
        }
    }

    private static WasmModule CreateMemorySizeAndGrowModule(
        bool imported,
        uint initialPages,
        uint? maximumPages
    )
    {
        return WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [0x7F]), ([0x7F], [0x7F])),
                    imported
                        ? HostLinkingModuleBinary.Imports(
                            (
                                "env",
                                "m",
                                2,
                                HostLinkingModuleBinary.Limits(initialPages, maximumPages + 2)
                            )
                        )
                        : [],
                    HostLinkingModuleBinary.Functions(0, 1),
                    imported ? [] : HostLinkingModuleBinary.Memories((initialPages, maximumPages)),
                    HostLinkingModuleBinary.Exports(
                        ("size", 0, 0),
                        ("grow", 0, 1),
                        ("memory", 2, 0)
                    ),
                    HostLinkingModuleBinary.Code(
                        ([], [0x3F, 0, 0x0B]),
                        ([], [0x20, 0, 0x40, 0, 0x0B])
                    )
                )
            )
            .Validate();
    }

    private static WasmValue MemoryValue(byte kind, ulong bits)
    {
        return kind switch
        {
            0x7F => WasmValue.FromI32(unchecked((int)bits)),
            0x7E => WasmValue.FromI64(unchecked((long)bits)),
            0x7D => WasmValue.FromF32Bits((uint)bits),
            0x7C => WasmValue.FromF64Bits(bits),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
