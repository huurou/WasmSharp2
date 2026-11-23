using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("1402", "unknown type 2")]
    [Arguments("4100D07014001A", "type mismatch")] // funcref
    [Arguments("4100D00114001A", "type mismatch")] // ref null 1
    [Arguments("4200D00014001A", "type mismatch")] // i32を要求する関数にi64を渡す
    public async Task Call_refの型番号か参照型か引数が不正である_検証時に命令位置と原因を返す(
        string instructions,
        string diagnostic
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], [0x7F]), ([], [])),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);
        var callOffset = (long)Array.IndexOf(binary, (byte)0x14);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert.That(exception.Location?.ByteOffset).IsEqualTo(callOffset);
            await Assert.That(exception.Location?.FunctionIndex).IsEqualTo(0u);
        }
    }
}
