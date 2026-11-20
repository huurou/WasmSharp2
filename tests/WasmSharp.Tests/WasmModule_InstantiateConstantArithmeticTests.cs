using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task Core3の整数定数式で桁あふれする_各演算を宣言型のビット幅で折り返す()
    {
        // Arrange
        // (最大値 + 2) × 3 − 5は、各型で加算・乗算・減算のすべてが範囲を超える。
        byte[] i32Operations = [0x41, 2, 0x6A, 0x41, 3, 0x6C, 0x41, 5, 0x6B];
        byte[] i64Operations = [0x42, 2, 0x7C, 0x42, 3, 0x7E, 0x42, 5, 0x7D];
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals(
                (0x7F, false, [0x41, .. HostLinkingModuleBinary.Signed(int.MaxValue), .. i32Operations, 0x0B]),
                (0x7E, false, [0x42, .. HostLinkingModuleBinary.Signed(long.MaxValue), .. i64Operations, 0x0B])
            ),
            HostLinkingModuleBinary.Exports(("i32", 3, 0), ("i64", 3, 1))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act
        var instance = module.Validate().Instantiate(new WasmExecutionContext(), []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instance.GetGlobal("i32").AsI32()).IsEqualTo(int.MaxValue - 1);
            await Assert.That(instance.GetGlobal("i64").AsI64()).IsEqualTo(long.MaxValue - 1);
        }
    }
}
