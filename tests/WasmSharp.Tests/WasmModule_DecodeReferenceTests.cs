using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("D07F0B", "malformed reference type", 1)]
    [Arguments("D0F0000B", "integer representation too long", 2)]
    [Arguments("D0", "unexpected end of section or function", 1)]
    [Arguments("D280808080100B", "integer too large", 5)]
    [Arguments("D280", "unexpected end of section or function", 2)]
    [Arguments("D070", "unexpected end of section or function", 2)]
    public async Task 参照即値や終端が不正_入力位置付きのDecode失敗として拒否する(
        string body,
        string prefix,
        int relativeOffset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
        );
        var expressionOffset = binary.Length - Convert.FromHexString(body).Length;

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(binary))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Decode, expressionOffset + relativeOffset, 0, 10)
                );
        }
    }
}
