using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments(2, 0, "0102010203040506")]
    [Arguments(0, 2, "0304050607080708")]
    [Arguments(0, 0, "0102030405060708")]
    public async Task メモリコピーの範囲が重なる_両方向と同一位置で元内容をコピーして結果を返さない(
        int destination,
        int source,
        string expected
    )
    {
        // Arrange
        var instance = WasmModule.Decode(CreateBulkMemoryModule(10)).Validate().Instantiate([]);
        var memory = instance.GetMemory("memory");
        memory.Write(65532, [1, 2, 3, 4, 5, 6, 7, 8]);
        var actual = new byte[8];

        // Act
        var result = instance
            .GetFunction("bulk")
            .Invoke([
                WasmValue.FromI32(65532 + destination),
                WasmValue.FromI32(65532 + source),
                WasmValue.FromI32(6),
            ]);
        memory.Read(65532, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values.IsEmpty).IsTrue();
            await Assert.That(Convert.ToHexString(actual)).IsEqualTo(expected);
        }
    }

    [Test]
    [Arguments(0x123, "CCCC23232323CCCC")]
    [Arguments(-1, "CCCCFFFFFFFFCCCC")]
    [Arguments(0x100, "CCCC00000000CCCC")]
    public async Task メモリの指定範囲を値で埋める_ページ境界をまたいで下位8ビットだけを書き込む(
        int value,
        string expected
    )
    {
        // Arrange
        var instance = WasmModule.Decode(CreateBulkMemoryModule(11)).Validate().Instantiate([]);
        var memory = instance.GetMemory("memory");
        memory.Write(65532, [0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC]);
        var actual = new byte[8];

        // Act
        var result = instance
            .GetFunction("bulk")
            .Invoke([WasmValue.FromI32(65534), WasmValue.FromI32(value), WasmValue.FromI32(4)]);
        memory.Read(65532, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values.IsEmpty).IsTrue();
            await Assert.That(Convert.ToHexString(actual)).IsEqualTo(expected);
        }
    }

    [Test]
    [Arguments((byte)10, 131072, 131072, 0, true)]
    [Arguments((byte)10, 0, 131072, 0, true)]
    [Arguments((byte)10, 131072, 0, 0, true)]
    [Arguments((byte)10, 131073, 0, 0, false)]
    [Arguments((byte)10, 0, 131073, 0, false)]
    [Arguments((byte)10, 131071, 0, 2, false)]
    [Arguments((byte)10, 0, 131071, 2, false)]
    [Arguments((byte)10, -1, 0, 0, false)]
    [Arguments((byte)10, 0, -1, 0, false)]
    [Arguments((byte)10, 0, 0, -1, false)]
    [Arguments((byte)10, int.MinValue, 0, 0, false)]
    [Arguments((byte)10, 0, int.MinValue, 0, false)]
    [Arguments((byte)10, 0, 0, int.MinValue, false)]
    [Arguments((byte)11, 131072, -1, 0, true)]
    [Arguments((byte)11, 131073, 0, 0, false)]
    [Arguments((byte)11, 131071, 0xAB, 2, false)]
    [Arguments((byte)11, -1, 0, 0, false)]
    [Arguments((byte)11, 0, 0xAB, -1, false)]
    [Arguments((byte)11, int.MinValue, 0, 0, false)]
    [Arguments((byte)11, 0, 0xAB, int.MinValue, false)]
    public async Task メモリ一括操作の全範囲を符号なしで検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする(
        byte opcode,
        int destination,
        int sourceOrValue,
        int length,
        bool succeeds
    )
    {
        // Arrange
        var bytes = CreateBulkMemoryModule(opcode);
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([]);
        var memory = instance.GetMemory("memory");
        var original = Enumerable.Range(0, 131072).Select(x => (byte)(x % 251 + 1)).ToArray();
        memory.Write(0, original);
        var arguments = new[]
        {
            WasmValue.FromI32(destination),
            WasmValue.FromI32(sourceOrValue),
            WasmValue.FromI32(length),
        };

        if (succeeds)
        {
            // Act
            var result = instance.GetFunction("bulk").Invoke(arguments);

            // Assert
            await Assert.That(result.Values.IsEmpty).IsTrue();
        }
        else
        {
            // Act & Assert
            var exception = await Assert
                .That(() => instance.GetFunction("bulk").Invoke(arguments))
                .ThrowsExactly<WasmTrapException>();
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
                        new(WasmProcessingStage.Invoke, Array.IndexOf(bytes, (byte)0xFC), 0)
                    );
            }
        }
        var actual = new byte[original.Length];
        memory.Read(0, actual);

        // Assert
        await Assert.That(actual.SequenceEqual(original)).IsTrue();
    }

    [Test]
    [Arguments((byte)10)]
    [Arguments((byte)11)]
    public async Task 空メモリに長さ0の一括操作を行う_各開始点0だけが成功する(byte opcode)
    {
        // Arrange
        var instance = WasmModule
            .Decode(CreateBulkMemoryModule(opcode, 0))
            .Validate()
            .Instantiate([]);

        // Act
        var result = instance
            .GetFunction("bulk")
            .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(0)]);

        // Assert
        await Assert.That(result.Values.IsEmpty).IsTrue();

        // Act & Assert
        await Assert
            .That(() =>
                instance
                    .GetFunction("bulk")
                    .Invoke([WasmValue.FromI32(1), WasmValue.FromI32(0), WasmValue.FromI32(0)])
            )
            .ThrowsExactly<WasmTrapException>();
        if (opcode == 10)
        {
            // Act & Assert
            await Assert
                .That(() =>
                    instance
                        .GetFunction("bulk")
                        .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(1), WasmValue.FromI32(0)])
                )
                .ThrowsExactly<WasmTrapException>();
        }
    }

    private static byte[] CreateBulkMemoryModule(byte opcode, uint pages = 2)
    {
        byte[] operation = opcode == 10 ? [0xFC, opcode, 0, 0] : [0xFC, opcode, 0];
        return HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7F, 0x7F], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((pages, null)),
            HostLinkingModuleBinary.Exports(("bulk", 0, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. operation, 0x0B]))
        );
    }
}
