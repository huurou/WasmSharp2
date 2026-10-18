using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments((byte)0x00, 16, "0102030405060708090A0B0C0D0E0F10")]
    [Arguments((byte)0x01, 8, "01000200030004000500060007000800")]
    [Arguments((byte)0x07, 1, "01010101010101010101010101010101")]
    [Arguments((byte)0x08, 2, "01020102010201020102010201020102")]
    [Arguments((byte)0x09, 4, "01020304010203040102030401020304")]
    [Arguments((byte)0x0A, 8, "01020304050607080102030405060708")]
    [Arguments((byte)0x5C, 4, "01020304000000000000000000000000")]
    [Arguments((byte)0x5D, 8, "01020304050607080000000000000000")]
    public async Task SIMDメモリ接続の各幅を末尾でloadする_非整列とoffsetを扱い両入力の公開4段階で結果bitを保つ(
        byte opcode,
        int width,
        string expected
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("run", 0, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0xFD, opcode, 0, 1, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        foreach (var module in modules)
        {
            var instance = module.Validate().Instantiate([]);
            instance
                .GetMemory("memory")
                .Write(
                    (ulong)(65536 - width),
                    Enumerable.Range(1, width).Select(x => (byte)x).ToArray()
                );
            var actual = instance
                .GetFunction("run")
                .Invoke([WasmValue.FromI32(65535 - width)])
                .Values;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(actual.Length).IsEqualTo(1);
                await Assert.That(actual[0].AsV128()).IsEqualTo(Vector(expected).AsV128());
            }
            // Act & Assert
            var trap = await Assert
                .That(() => instance.GetFunction("run").Invoke([WasmValue.FromI32(65536 - width)]))
                .ThrowsExactly<WasmTrapException>();
            await Assert.That(trap!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert
                .That(trap.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Invoke,
                        module.Functions[0].Instructions[1].ByteOffset,
                        0
                    )
                );
        }
    }

    [Test]
    [Arguments((byte)0x54, (byte)0x58, 1, (byte)15)]
    [Arguments((byte)0x55, (byte)0x59, 2, (byte)7)]
    [Arguments((byte)0x56, (byte)0x5A, 4, (byte)3)]
    [Arguments((byte)0x57, (byte)0x5B, 8, (byte)1)]
    public async Task SIMDメモリ接続のlaneを末尾で転送する_同じ型順で非対象laneと非対象memoryとstoreの結果個数を保つ(
        byte load,
        byte store,
        int width,
        byte lane
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7B], [0x7B]), ([0x7F, 0x7B], [])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("load", 0, 0), ("store", 0, 1), ("memory", 2, 0)),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x20, 1, 0xFD, load, 0, 0, lane, 0x0B]),
                ([], [0x20, 0, 0x20, 1, 0xFD, store, 0, 0, lane, 0x0B])
            )
        );
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([]);
        var memory = instance.GetMemory("memory");
        var inputBytes = Enumerable.Range(1, 16).Select(x => (byte)x).ToArray();
        var initial = Enumerable.Repeat((byte)0xA5, 16).ToArray();
        memory.Write(65520, initial);

        // Act
        var loaded = instance
            .GetFunction("load")
            .Invoke([WasmValue.FromI32(65536 - width), Vector(Convert.ToHexString(inputBytes))])
            .Values[0];
        var stored = instance
            .GetFunction("store")
            .Invoke([WasmValue.FromI32(65536 - width), Vector(Convert.ToHexString(inputBytes))])
            .Values;
        var actualMemory = new byte[16];
        memory.Read(65520, actualMemory);
        var expectedVector = inputBytes.ToArray();
        initial.AsSpan(16 - width).CopyTo(expectedVector.AsSpan(lane * width));
        var expectedMemory = initial.ToArray();
        inputBytes.AsSpan(lane * width, width).CopyTo(expectedMemory.AsSpan(16 - width));

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(loaded.AsV128())
                .IsEqualTo(Vector(Convert.ToHexString(expectedVector)).AsV128());
            await Assert.That(stored).IsEmpty();
            await Assert.That(actualMemory.SequenceEqual(expectedMemory)).IsTrue();
        }
        // Act & Assert
        await Assert
            .That(() =>
                instance
                    .GetFunction("store")
                    .Invoke([
                        WasmValue.FromI32(65537 - width),
                        Vector(Convert.ToHexString(inputBytes)),
                    ])
            )
            .ThrowsExactly<WasmTrapException>();
        memory.Read(65520, actualMemory);
        await Assert.That(actualMemory.SequenceEqual(expectedMemory)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SIMDメモリ接続のstoreがInvokeかstartでtrapする_段階と元位置と先行更新と保存参照と次の呼出しを保つ(
        bool start
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1));
        memory.Write(65535, [0xA5]);
        WasmInstance? saved = null;
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define("memory", memory);
        host.Define(
            "save",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    saved = instance;
                    calls++;
                    return new([]);
                }
            )
        );
        var code = HostLinkingModuleBinary.Code(
            (
                [],
                [
                    0x41,
                    0,
                    0x41,
                    33,
                    0x3A,
                    0,
                    0,
                    0x10,
                    0,
                    0x41,
                    .. HostLinkingModuleBinary.Signed(65535),
                    0xFD,
                    0x0C,
                    .. new byte[16],
                    0xFD,
                    0x0B,
                    0,
                    0,
                    0x10,
                    0,
                    0x0B,
                ]
            ),
            ([], [0x41, 0, 0x2D, 0, 0, 0x0B])
        );
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(
                ("env", "memory", 2, HostLinkingModuleBinary.Limits(1)),
                ("env", "save", 0, [0])
            ),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Exports(("run", 0, 1), ("read", 0, 2), ("memory", 2, 0)),
            start ? HostLinkingModuleBinary.Start(1) : [],
            code
        );
        var module = WasmModule.Decode(bytes).Validate();
        var offset = bytes.Length - code.Length + Array.LastIndexOf(code, (byte)0xFD);
        WasmInstance? returned = null;
        Action run = start
            ? () => returned = module.Instantiate([host])
            : () => (returned = module.Instantiate([host])).GetFunction("run").Invoke([]);

        // Act & Assert
        var trap = await Assert.That(run).ThrowsExactly<WasmTrapException>();
        await Assert.That(saved).IsNotNull();

        // Act
        var result = saved!.GetFunction("read").Invoke([]).Values[0].AsI32();
        var last = new byte[1];
        memory.Read(65535, last);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(trap!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
            await Assert
                .That(
                    trap.Message.StartsWith("out of bounds memory access", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert
                .That(trap.Location)
                .IsEqualTo(
                    new(
                        start ? WasmProcessingStage.Instantiate : WasmProcessingStage.Invoke,
                        offset,
                        1
                    )
                );
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(result).IsEqualTo(33);
            await Assert.That(last[0]).IsEqualTo((byte)0xA5);
            await Assert.That(saved.GetMemory("memory")).IsSameReferenceAs(memory);
            await Assert.That(returned is null).IsEqualTo(start);
        }
    }
}
