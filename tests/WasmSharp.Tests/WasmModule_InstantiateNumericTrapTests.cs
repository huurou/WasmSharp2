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
    public async Task Startで数値trapに到達する_原因別の公開診断とInstantiateの元位置を保ち以後の副作用を実行しない(
        string numericInstructions,
        WasmTrapReason reason,
        string expectedMessage
    )
    {
        // Arrange
        var called = false;
        var shared = new WasmGlobal(new(WasmValueKind.I32, true), WasmValue.FromI32(0));
        var host = new WasmHostModule("env");
        host.Define("shared", shared);
        host.Define(
            "after",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    called = true;
                    return new([]);
                }
            )
        );
        byte[] afterTrap = [0x1A, 0x41, 0x2A, 0x24, 0, 0x10, 0, 0x0B];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(
                ("env", "after", 0, [0]),
                ("env", "shared", 3, [0x7F, 1])
            ),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Start(1),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    [0x41, 7, 0x24, 0, .. Convert.FromHexString(numericInstructions), .. afterTrap]
                )
            )
        );
        var module = WasmModule.Decode(bytes).Validate();
        var expectedOffset = bytes.Length - afterTrap.Length - 1;

        // Act & Assert
        var exception = await Assert
            .That(() => module.Instantiate([host]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(reason);
            await Assert
                .That(exception.Message.StartsWith(expectedMessage, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(WasmProcessingStage.Instantiate, expectedOffset, 1)
                );
            await Assert.That(shared.Value.AsI32()).IsEqualTo(7);
            await Assert.That(called).IsFalse();
        }
    }
}
