using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task V128定数でglobalを初期化する_両入力の4段階で初期値をそのまま取得する(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Globals(
                (0x7B, true, Convert.FromHexString("FD0CBC9A78563412F87F00000000000000800B"))
            ),
            HostLinkingModuleBinary.Exports(("value", 3, 0), ("read", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x23, 0, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        Array.Clear(bytes);

        // Act
        var instance = module.Validate().Instantiate([]);
        var global = instance.GetGlobalResource("value");
        var result = instance.GetFunction("read").Invoke([]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(global.Type).IsEqualTo(new WasmGlobalType(WasmValueKind.V128, true));
            await Assert
                .That(global.Value.AsV128())
                .IsEqualTo((0x7FF8123456789ABCUL, 0x8000000000000000UL));
            await Assert.That(result.Values[0].AsV128()).IsEqualTo(global.Value.AsV128());
        }
    }
}
