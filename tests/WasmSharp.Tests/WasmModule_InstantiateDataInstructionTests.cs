using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments((byte)10)]
    [Arguments((byte)11)]
    public async Task 開始関数のメモリ一括操作が範囲外になる_共有メモリを変更せずInstantiateと元命令位置を持つtrapになる(
        byte opcode
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var original = Enumerable.Range(0, 65536).Select(x => (byte)(x % 251 + 1)).ToArray();
        memory.Write(0, original);
        var host = new WasmHostModule("env");
        host.Define("m", memory);
        byte[] operation = opcode == 10 ? [0xFC, opcode, 0, 0] : [0xFC, opcode, 0];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    [
                        0x41,
                        .. HostLinkingModuleBinary.Signed(65535),
                        0x41,
                        0,
                        0x41,
                        2,
                        .. operation,
                        0x0B,
                    ]
                )
            )
        );
        var module = WasmModule.Decode(bytes).Validate();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmTrapException>();
        var actual = new byte[original.Length];
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
                .IsEqualTo(
                    new(WasmProcessingStage.Instantiate, bytes.Length - operation.Length - 1, 0)
                );
            await Assert.That(actual.SequenceEqual(original)).IsTrue();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task 開始関数が初期化済みのアクティブデータを参照する_破棄済みとして扱い空コピーだけ成功する(
        int length
    )
    {
        // Arrange
        var memory = new WasmMemory(new(1, null));
        var host = new WasmHostModule("env");
        host.Define("m", memory);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(
                ([], [0x41, 0, 0x41, 0, 0x41, (byte)length, 0xFC, 8, 0, 0, 0x0B])
            ),
            HostLinkingModuleBinary.Section(11, [1, 0, 0x41, 0, 0x0B, 1, 0xAB])
        );
        var module = WasmModule.Decode(bytes).Validate();
        if (length == 0)
        {
            // Act
            var instance = module.Instantiate([host]);

            // Assert
            await Assert.That(instance).IsNotNull();
        }
        else
        {
            // Act & Assert
            var exception = await Assert
                .That(() => module.Instantiate([host]))
                .ThrowsExactly<WasmTrapException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(WasmProcessingStage.Instantiate, Array.IndexOf(bytes, (byte)0xFC), 0)
                    );
            }
        }
        var actual = new byte[1];
        memory.Read(0, actual);

        // Assert
        await Assert.That(actual[0]).IsEqualTo((byte)0xAB);
    }
}
