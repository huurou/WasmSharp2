using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("4100D06F4200FC1101")] // table.fillの開始位置がi32
    [Arguments("4200D06F4100FC1101")] // table.fillの個数がi32
    [Arguments("420042004100FC0E0101")] // 64→64の個数がi32
    [Arguments("410041004100FC0E0100")] // 32→64の転送先がi32
    [Arguments("420042004100FC0E0100")] // 32→64の転送元がi64
    [Arguments("420041004200FC0E0100")] // 32→64の個数がi64
    public async Task 充填またはコピーの添字と個数の幅を誤る_型不一致として検証時に拒否する(string body)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(4, [2, 0x6F, 0, 1, 0x6F, 4, 1]),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(body), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }
}
