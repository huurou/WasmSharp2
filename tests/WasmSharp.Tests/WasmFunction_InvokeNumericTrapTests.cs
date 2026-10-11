using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmFunction_InvokeTests
{
    [Test]
    [Arguments("410141006D", WasmTrapReason.IntegerDivideByZero, "integer divide by zero")]
    [Arguments("418080808078417F6D", WasmTrapReason.IntegerOverflow, "integer overflow")]
    [Arguments(
        "430000C07FA8",
        WasmTrapReason.InvalidConversionToInteger,
        "invalid conversion to integer"
    )]
    public async Task 数値trapに到達する_原因別の公開診断とInvokeの元位置を保ち以後の副作用を実行しない(
        string numericInstructions,
        WasmTrapReason reason,
        string expectedMessage
    )
    {
        // Arrange
        var called = false;
        var host = new WasmHostModule("env");
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
            HostLinkingModuleBinary.Imports(("env", "after", 0, [0])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0, 0x0B])),
            HostLinkingModuleBinary.Exports(("run", 0, 1), ("value", 3, 0)),
            HostLinkingModuleBinary.Code(
                (
                    [],
                    [0x41, 7, 0x24, 0, .. Convert.FromHexString(numericInstructions), .. afterTrap]
                )
            )
        );
        var instance = WasmModule.Decode(bytes).Validate().Instantiate([host]);
        var function = instance.GetFunction("run");
        var expectedOffset = bytes.Length - afterTrap.Length - 1;

        // Act & Assert
        var exception = await Assert
            .That(() => function.Invoke([]))
            .ThrowsExactly<WasmTrapException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Reason).IsEqualTo(reason);
            await Assert
                .That(exception.Message.StartsWith(expectedMessage, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Invoke, expectedOffset, 1));
            await Assert.That(instance.GetGlobal("value").AsI32()).IsEqualTo(7);
            await Assert.That(called).IsFalse();
        }
    }
}
