using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 各階層のnullを変換してglobalを初期化する_変換先の階層のnullを取得できる()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals(
                (0x6E, false, [0xD0, 0x72, 0xFB, 0x1A, 0x0B]),
                (0x6F, false, [0xD0, 0x71, 0xFB, 0x1B, 0x0B])
            ),
            HostLinkingModuleBinary.Exports(("internal", 3, 0), ("external", 3, 1))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act
        var instance = module.Validate().Instantiate(new WasmExecutionContext(), []);
        var internalNull = instance.GetGlobal("internal");
        var externalNull = instance.GetGlobal("external");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(internalNull.Kind).IsEqualTo(WasmValueKind.AnyRef);
            await Assert.That(internalNull.IsNullReference).IsTrue();
            await Assert.That(externalNull.Kind).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(externalNull.IsNullReference).IsTrue();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ホストの数値を指す非nullの外部参照でglobalを初期化する_内部への変換と往復で同じホスト参照を保持する(
        bool boxedWasmValue
    )
    {
        // Arrange
        var hostValue = boxedWasmValue ? (object)WasmValue.FromI31(7) : 7;
        var host = new WasmHostModule("env");
        host.Define(
            "value",
            new WasmGlobal(
                new WasmGlobalType(WasmValueType.Reference(WasmValueKind.ExternRef, false), false),
                WasmValue.FromExternRef(hostValue)
            )
        );
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("env", "value", 3, [0x64, 0x6F, 0])),
            HostLinkingModuleBinary.Section(
                6,
                [
                    2,
                    // (global (ref any) (any.convert_extern (global.get 0)))
                    0x64,
                    0x6E,
                    0,
                    0x23,
                    0,
                    0xFB,
                    0x1A,
                    0x0B,
                    // (global (ref extern) (extern.convert_any (any.convert_extern (global.get 0))))
                    0x64,
                    0x6F,
                    0,
                    0x23,
                    0,
                    0xFB,
                    0x1A,
                    0xFB,
                    0x1B,
                    0x0B,
                ]
            ),
            HostLinkingModuleBinary.Exports(("internal", 3, 1), ("external", 3, 2))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act
        var instance = module.Validate().Instantiate(new WasmExecutionContext(), [host]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.GetGlobal("internal").AsHostRef()).IsSameReferenceAs(hostValue);
            await Assert.That(instance.GetGlobal("external").AsExternRef()).IsSameReferenceAs(hostValue);
        }
    }
}
