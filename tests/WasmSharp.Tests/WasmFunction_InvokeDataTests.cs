using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    public async Task メモリのないmoduleでパッシブデータを繰り返し破棄する_引数も結果もなく成功する()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("drop", 0, 0)),
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(([], [0xFC, 9, 0, 0x0B])),
            HostLinkingModuleBinary.Section(11, [1, 1, 2, 0xAB, 0xCD])
        );
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([]);

        // Act
        var first = instance.GetFunction("drop").Invoke([]);
        var second = instance.GetFunction("drop").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Values.IsEmpty).IsTrue();
            await Assert.That(second.Values.IsEmpty).IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task パッシブデータを部分コピーして再利用する_指定範囲だけを書き込み元データを保持する(
        bool imported
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7F, 0x7F], [])),
            imported ? HostLinkingModuleBinary.Imports(("env", "m", 2, [0, 1])) : [],
            HostLinkingModuleBinary.Functions(0),
            imported ? [] : HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("init", 0, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, 0xFC, 8, 0, 0, 0x0B])),
            HostLinkingModuleBinary.Section(11, [1, 1, 4, 0x11, 0x22, 0x33, 0x44])
        );
        var hostMemory = new WasmMemory(new(1, null));
        var host = new WasmHostModule("env");
        host.Define("m", hostMemory);
        var instance = WasmModule.Decode(bytes).Validate().Instantiate(imported ? [host] : []);
        var memory = instance.GetMemory("memory");
        var original = new byte[8];
        memory.Read(0, original);
        memory.Write(0, [0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC]);

        // Act
        var first = instance
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(1), WasmValue.FromI32(1), WasmValue.FromI32(2)]);
        var second = instance
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(4), WasmValue.FromI32(0), WasmValue.FromI32(4)]);
        var actual = new byte[8];
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Convert.ToHexString(original)).IsEqualTo("0000000000000000");
            await Assert.That(first.Values.IsEmpty).IsTrue();
            await Assert.That(second.Values.IsEmpty).IsTrue();
            await Assert.That(Convert.ToHexString(actual)).IsEqualTo("CC2233CC11223344");
            if (imported)
            {
                await Assert.That(memory).IsSameReferenceAs(hostMemory);
            }
        }
    }

    [Test]
    public async Task 同じmoduleの一方のinstanceだけでデータを破棄する_他方の内容を保ち破棄済みデータは空として扱う()
    {
        // Arrange
        var module = WasmModule.Decode(CreateDataInitializationModule()).Validate();
        var first = module.Instantiate([]);
        var second = module.Instantiate([]);

        // Act
        first.GetFunction("drop").Invoke([]);
        first.GetFunction("drop").Invoke([]);
        first
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(65536), WasmValue.FromI32(0), WasmValue.FromI32(0)]);
        second
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(4)]);
        var actual = new byte[4];
        second.GetMemory("memory").Read(0, actual);

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo("11223344");

        // Act & Assert
        await Assert
            .That(() =>
                first
                    .GetFunction("init")
                    .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(1)])
            )
            .ThrowsExactly<WasmTrapException>();
    }

    [Test]
    [Arguments(false, 65536, 4, 0, true)]
    [Arguments(false, 65537, 0, 0, false)]
    [Arguments(false, 0, 5, 0, false)]
    [Arguments(false, 65535, 0, 2, false)]
    [Arguments(false, 0, 3, 2, false)]
    [Arguments(false, -1, 0, 0, false)]
    [Arguments(false, 0, -1, 0, false)]
    [Arguments(false, 1, 0, -1, false)]
    [Arguments(false, 0, 1, -1, false)]
    [Arguments(true, 0, 0, 0, true)]
    [Arguments(true, 65536, 0, 0, true)]
    [Arguments(true, 65537, 0, 0, false)]
    [Arguments(true, 0, 1, 0, false)]
    [Arguments(true, 0, 0, 1, false)]
    public async Task memory_initの全範囲と破棄状態を検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする(
        bool dropped,
        int destination,
        int source,
        int length,
        bool succeeds
    )
    {
        // Arrange
        var bytes = CreateDataInitializationModule();
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([]);
        var memory = instance.GetMemory("memory");
        var original = Enumerable.Range(0, 65536).Select(x => (byte)(x % 251 + 1)).ToArray();
        memory.Write(0, original);
        if (dropped)
        {
            instance.GetFunction("drop").Invoke([]);
        }
        var arguments = new[]
        {
            WasmValue.FromI32(destination),
            WasmValue.FromI32(source),
            WasmValue.FromI32(length),
        };

        if (succeeds)
        {
            // Act
            var result = instance.GetFunction("init").Invoke(arguments);

            // Assert
            await Assert.That(result.Values.IsEmpty).IsTrue();
        }
        else
        {
            // Act & Assert
            var exception = await Assert
                .That(() => instance.GetFunction("init").Invoke(arguments))
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
    public async Task アクティブデータは初期化後も同じインデックスで参照できる_空データとして扱い繰り返し破棄できる()
    {
        // Arrange
        var instance = WasmModule
            .Decode(CreateDataInitializationModule(true))
            .Validate()
            .Instantiate([]);
        var memory = instance.GetMemory("memory");
        var actual = new byte[4];
        memory.Read(2, actual);

        // Act
        instance
            .GetFunction("init")
            .Invoke([WasmValue.FromI32(65536), WasmValue.FromI32(0), WasmValue.FromI32(0)]);
        instance.GetFunction("drop").Invoke([]);
        instance.GetFunction("drop").Invoke([]);

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo("11223344");

        // Act & Assert
        await Assert
            .That(() =>
                instance
                    .GetFunction("init")
                    .Invoke([WasmValue.FromI32(0), WasmValue.FromI32(0), WasmValue.FromI32(1)])
            )
            .ThrowsExactly<WasmTrapException>();
    }

    private static byte[] CreateDataInitializationModule(bool active = false)
    {
        return HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7F, 0x7F], []), ([], [])),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("init", 0, 0), ("drop", 0, 1), ("memory", 2, 0)),
            HostLinkingModuleBinary.Section(12, [2]),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x20, 1, 0x20, 2, 0xFC, 8, 1, 0, 0x0B]),
                ([], [0xFC, 9, 1, 0x0B])
            ),
            HostLinkingModuleBinary.Section(
                11,
                [
                    2,
                    1,
                    1,
                    0x99,
                    .. active ? new byte[] { 0, 0x41, 2, 0x0B } : [1],
                    4,
                    0x11,
                    0x22,
                    0x33,
                    0x44,
                ]
            )
        );
    }
}
