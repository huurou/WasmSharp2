using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 初期化とメモリ書き込み後のStartが失敗する_元の失敗と保存参照から初期化済みメモリを使える(
        bool hostFailure
    )
    {
        // Arrange
        var expected = new InvalidOperationException("ホスト処理の失敗");
        var memory = new WasmMemory(new(1));
        WasmInstance? savedInstance = null;
        WasmFunction? savedFunction = null;
        WasmMemory? savedMemory = null;
        WasmInstance? returned = null;
        var calls = 0;
        var observed = new byte[3];
        var host = new WasmHostModule("env");
        host.Define("memory", memory);
        host.Define(
            "save",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    calls++;
                    savedInstance = instance;
                    savedFunction = instance.GetFunction("read");
                    savedMemory = instance.GetMemory("memory");
                    savedMemory.Read(0, observed);
                    if (hostFailure)
                    {
                        throw expected;
                    }
                    return new([]);
                }
            )
        );
        var code = HostLinkingModuleBinary.Code(
            (
                [],
                [
                    0x41,
                    2,
                    0x41,
                    33,
                    0x3A,
                    0,
                    0,
                    0x10,
                    0,
                    0x41,
                    .. HostLinkingModuleBinary.Signed(65535),
                    0x28,
                    2,
                    0,
                    0x1A,
                    0x0B,
                ]
            ),
            ([], [0x41, 0, 0x28, 2, 0, 0x0B])
        );
        var data = HostLinkingModuleBinary.Section(11, [1, 0, 0x41, 0, 0x0B, 2, 11, 22]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(
                ("env", "memory", 2, HostLinkingModuleBinary.Limits(1)),
                ("env", "save", 0, [0])
            ),
            HostLinkingModuleBinary.Functions(0, 1),
            HostLinkingModuleBinary.Exports(("memory", 2, 0), ("read", 0, 2)),
            HostLinkingModuleBinary.Start(1),
            code,
            data
        );
        var module = WasmModule.Decode(bytes).Validate();
        var trapOffset = bytes.Length - data.Length - code.Length + Array.IndexOf(code, (byte)0x28);

        // Act & Assert
        var exception = await Assert
            .That(() => returned = module.Instantiate([host]))
            .Throws<Exception>();
        await Assert.That(savedInstance).IsNotNull();
        await Assert.That(savedFunction).IsNotNull();
        await Assert.That(savedMemory).IsNotNull();

        // Act
        var initializedValue = savedFunction!.Invoke([]).Values[0].AsI32();
        savedMemory!.Write(0, [44]);
        var updatedValue = savedInstance!.GetFunction("read").Invoke([]).Values[0].AsI32();
        var actual = new byte[3];
        memory.Read(0, actual);

        // Assert
        using (Assert.Multiple())
        {
            if (hostFailure)
            {
                await Assert.That(exception).IsSameReferenceAs(expected);
            }
            else
            {
                await Assert.That(exception).IsTypeOf<WasmTrapException>();
                var trap = (WasmTrapException)exception!;
                await Assert.That(trap.Reason).IsEqualTo(WasmTrapReason.MemoryOutOfBounds);
                await Assert
                    .That(
                        trap.Message.StartsWith(
                            "out of bounds memory access",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
                await Assert
                    .That(trap.Location)
                    .IsEqualTo(new(WasmProcessingStage.Instantiate, trapOffset, 1));
            }
            await Assert.That(returned).IsNull();
            await Assert.That(calls).IsEqualTo(1);
            await Assert.That(savedMemory).IsSameReferenceAs(memory);
            await Assert.That(savedInstance.GetMemory("memory")).IsSameReferenceAs(memory);
            await Assert.That(savedInstance.GetFunction("read")).IsSameReferenceAs(savedFunction);
            await Assert.That(observed.SequenceEqual(new byte[] { 11, 22, 33 })).IsTrue();
            await Assert.That(initializedValue).IsEqualTo(0x0021160B);
            await Assert.That(updatedValue).IsEqualTo(0x0021162C);
            await Assert.That(actual.SequenceEqual(new byte[] { 44, 22, 33 })).IsTrue();
        }
    }
}
