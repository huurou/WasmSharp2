using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 要素またはdata初期化が失敗する_残った関数を共有tableから間接callでき宣言順dropを保持する(
        bool elementFailure
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.FuncRef, new(4));
        var memory = new WasmMemory(new(1));
        memory.Write(0, [99]);
        var starts = 0;
        var host = new WasmHostModule("env");
        host.Define("t", table);
        host.Define("m", memory);
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
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(
                        ("env", "t", 1, [0x70, 0, 4]),
                        ("env", "m", 2, [0, 1]),
                        ("env", "start", 0, [0])
                    ),
                    HostLinkingModuleBinary.Functions(1, 0, 0),
                    HostLinkingModuleBinary.Globals((0x7F, false, [0x41, 42, 0x0B])),
                    HostLinkingModuleBinary.Start(0),
                    HostLinkingModuleBinary.Section(
                        9,
                        [
                            5,
                            3,
                            0,
                            1,
                            1,
                            0,
                            0x41,
                            0,
                            0x0B,
                            3,
                            1,
                            2,
                            3,
                            3,
                            0,
                            1,
                            1,
                            0,
                            0x41,
                            4,
                            0x0B,
                            .. (elementFailure ? new byte[] { 1, 1 } : [0]),
                            3,
                            0,
                            1,
                            1,
                        ]
                    ),
                    HostLinkingModuleBinary.Code(
                        ([], [0x23, 0, 0x41, 0, 0x2D, 0, 0, 0x6A, 0x0B]),
                        ([], [0x41, 3, 0x41, 0, 0x41, 1, 0xFC, 0x0C, 0, 0, 0x0B]),
                        ([], [0x41, 3, 0x41, 0, 0x41, 1, 0xFC, 0x0C, 4, 0, 0x0B])
                    ),
                    HostLinkingModuleBinary.Section(
                        11,
                        [1, 0, 0x41, .. HostLinkingModuleBinary.Signed(65536), 0x0B, 1, 22]
                    )
                )
            )
            .Validate();
        var caller = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
                    HostLinkingModuleBinary.Imports(("env", "t", 1, [0x70, 0, 4])),
                    HostLinkingModuleBinary.Functions(1, 0, 0),
                    HostLinkingModuleBinary.Exports(
                        ("read", 0, 0),
                        ("dropped", 0, 1),
                        ("later", 0, 2)
                    ),
                    HostLinkingModuleBinary.Code(
                        ([], [0x41, 0, 0x11, 1, 0, 0x0B]),
                        ([], [0x41, 1, 0x11, 0, 0, 0x0B]),
                        ([], [0x41, 2, 0x11, 0, 0, 0x0B])
                    )
                )
            )
            .Validate()
            .Instantiate([host]);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmTrapException>();
        await Assert
            .That(exception!.Reason)
            .IsEqualTo(
                elementFailure ? WasmTrapReason.TableOutOfBounds : WasmTrapReason.MemoryOutOfBounds
            );
        await Assert.That(exception.Location!.Stage).IsEqualTo(WasmProcessingStage.Instantiate);
        await Assert.That(exception.Location!.SectionId).IsEqualTo(elementFailure ? (byte?)9 : 11);
        await Assert.That(caller.GetFunction("read").Invoke([]).Values[0].AsI32()).IsEqualTo(141);
        await Assert.That(starts).IsEqualTo(0);
        await Assert
            .That(() => caller.GetFunction("dropped").Invoke([]))
            .ThrowsExactly<WasmTrapException>();
        await Assert.That(table.Get(3).AsFuncRef()).IsNull();
        if (elementFailure)
        {
            await Assert.That(() => caller.GetFunction("later").Invoke([])).ThrowsNothing();
            await Assert.That(table.Get(3).AsFuncRef()).IsSameReferenceAs(table.Get(0).AsFuncRef());
            await Assert
                .That(table.Get(3).AsFuncRef()!.Invoke([]).Values[0].AsI32())
                .IsEqualTo(141);
        }
        else
        {
            await Assert
                .That(() => caller.GetFunction("later").Invoke([]))
                .ThrowsExactly<WasmTrapException>();
            await Assert.That(table.Get(3).AsFuncRef()).IsNull();
        }
        await Assert.That(caller.GetFunction("read").Invoke([]).Values[0].AsI32()).IsEqualTo(141);
    }
}
