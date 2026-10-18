using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmInstance_ConstructorTests
{
    [Test]
    public async Task Import済みglobalを渡す_定義globalを評価してinstanceごとに構築する()
    {
        // Arrange
        var imported = new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(17));
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0])),
                    HostLinkingModuleBinary.Globals(
                        (0x7F, true, [0x23, 0, 0x0B]),
                        (0x7F, false, [0x41, 42, 0x0B])
                    ),
                    HostLinkingModuleBinary.Exports(
                        ("imported", 3, 0),
                        ("copied", 3, 1),
                        ("constant", 3, 2)
                    )
                )
            )
            .Validate();

        // Act
        var first = new WasmInstance(module, WasmExecutionOptions.Default, [], [imported], [], []);
        var second = new WasmInstance(module, WasmExecutionOptions.Default, [], [imported], [], []);

        // Assert
        await Assert.That(first.Globals.Length).IsEqualTo(3);
        await Assert.That(second.Globals.Length).IsEqualTo(3);
        using (Assert.Multiple())
        {
            await Assert.That(first.GetGlobalResource("imported")).IsSameReferenceAs(imported);
            await Assert.That(second.GetGlobalResource("imported")).IsSameReferenceAs(imported);
            await Assert.That(first.GetGlobal("copied").AsI32()).IsEqualTo(17);
            await Assert.That(first.GetGlobal("constant").AsI32()).IsEqualTo(42);
            await Assert
                .That(
                    ReferenceEquals(
                        first.GetGlobalResource("copied"),
                        second.GetGlobalResource("copied")
                    )
                )
                .IsFalse();
        }
    }
}
