using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments("410141006D", WasmTrapReason.IntegerDivideByZero, "integer divide by zero")]
    [Arguments("418080808078417F6D", WasmTrapReason.IntegerOverflow, "integer overflow")]
    [Arguments(
        "430000C07FA8",
        WasmTrapReason.InvalidConversionToInteger,
        "invalid conversion to integer"
    )]
    public async Task 制御内のStartで数値trapに到達する_公開診断と失敗前の更新と保存参照を保ち以後の副作用を実行しない(
        string numericInstructions,
        WasmTrapReason reason,
        string expectedMessage
    )
    {
        // Arrange
        var afterCalled = false;
        var saveCalls = 0;
        var shared = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var element = new object();
        WasmInstance? savedInstance = null;
        WasmFunction? savedFunction = null;
        WasmMemory? savedMemory = null;
        WasmTable? savedTable = null;
        WasmGlobal? savedGlobal = null;
        WasmInstance? returned = null;
        var host = new WasmHostModule("env");
        host.Define("shared", shared);
        host.Define(
            "save",
            WasmFunction.CreateHost(
                new([], []),
                (instance, _) =>
                {
                    saveCalls++;
                    savedInstance = instance;
                    savedFunction = instance.GetFunction("read");
                    savedMemory = instance.GetMemory("memory");
                    savedTable = instance.GetTable("table");
                    savedGlobal = instance.GetGlobalResource("value");
                    savedMemory.Write(0, [9]);
                    savedTable.Set(0, WasmValue.FromExternRef(element));
                    return new([]);
                }
            )
        );
        host.Define(
            "after",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    afterCalled = true;
                    return new([]);
                }
            )
        );
        byte[] afterTrap = [0x1A, 0x41, 0x2A, 0x24, 0, 0x41, 0, 0x24, 1, 0x10, 1, 0x0B, 0x0B];
        // 初期化済みglobalが42なら、数値計算で共有globalと定義globalを更新して参照を保存する。
        byte[] beforeTrap =
        [
            0x23,
            1,
            0x41,
            0x2A,
            0x46,
            0x04,
            0x40,
            0x23,
            0,
            0x41,
            7,
            0x6A,
            0x24,
            0,
            0x23,
            1,
            0x41,
            1,
            0x6A,
            0x24,
            1,
            0x10,
            0,
        ];
        byte[] readInstructions = [0x23, 1, 0x0B];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(
                ("env", "save", 0, [0]),
                ("env", "after", 0, [0]),
                ("env", "shared", 3, [0x7F, 1])
            ),
            HostLinkingModuleBinary.Functions(1, 0),
            HostLinkingModuleBinary.Tables((0x6F, 1, null)),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x2A, 0x0B])),
            HostLinkingModuleBinary.Exports(
                ("read", 0, 2),
                ("memory", 2, 0),
                ("table", 1, 0),
                ("value", 3, 1),
                ("shared", 3, 0)
            ),
            HostLinkingModuleBinary.Start(3),
            HostLinkingModuleBinary.Code(
                ([], readInstructions),
                ([], [.. beforeTrap, .. Convert.FromHexString(numericInstructions), .. afterTrap])
            )
        );
        var module = WasmModule.Decode(bytes).Validate();
        var expectedOffset = bytes.Length - afterTrap.Length - 1;

        // Act & Assert
        var exception = await Assert
            .That(() => returned = module.Instantiate([host]))
            .ThrowsExactly<WasmTrapException>();
        await Assert.That(savedInstance).IsNotNull();

        // Act
        var valueAfterTrap = savedFunction!.Invoke([]).Values[0].AsI32();
        savedGlobal!.Value = WasmValue.FromI32(44);
        var updatedValue = savedInstance!.GetFunction("read").Invoke([]).Values[0].AsI32();
        byte[] written = [0];
        savedMemory!.Read(0, written);
        savedMemory.Write(0, [10]);
        byte[] updated = [0];
        savedInstance.GetMemory("memory").Read(0, updated);
        var savedElement = savedTable!.Get(0).AsExternRef();
        var updatedElement = new object();
        savedTable.Set(0, WasmValue.FromExternRef(updatedElement));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(reason);
            await Assert
                .That(exception.Message.StartsWith(expectedMessage, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(WasmProcessingStage.Instantiate, expectedOffset, 3)
                );
            await Assert.That(returned).IsNull();
            await Assert.That(shared.Value.AsI32()).IsEqualTo(7);
            await Assert.That(saveCalls).IsEqualTo(1);
            await Assert.That(afterCalled).IsFalse();
            await Assert.That(valueAfterTrap).IsEqualTo(43);
            await Assert.That(updatedValue).IsEqualTo(44);
            await Assert.That(written[0]).IsEqualTo((byte)9);
            await Assert.That(updated[0]).IsEqualTo((byte)10);
            await Assert.That(ReferenceEquals(savedElement, element)).IsTrue();
            await Assert
                .That(
                    ReferenceEquals(
                        savedInstance.GetTable("table").Get(0).AsExternRef(),
                        updatedElement
                    )
                )
                .IsTrue();
            await Assert
                .That(ReferenceEquals(savedFunction, savedInstance.GetFunction("read")))
                .IsTrue();
            await Assert
                .That(ReferenceEquals(savedMemory, savedInstance.GetMemory("memory")))
                .IsTrue();
            await Assert
                .That(ReferenceEquals(savedTable, savedInstance.GetTable("table")))
                .IsTrue();
            await Assert
                .That(ReferenceEquals(savedGlobal, savedInstance.GetGlobalResource("value")))
                .IsTrue();
            await Assert
                .That(ReferenceEquals(shared, savedInstance.GetGlobalResource("shared")))
                .IsTrue();
        }
    }
}
