using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
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
            memory.Write(3, Convert.FromHexString(initial));
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
