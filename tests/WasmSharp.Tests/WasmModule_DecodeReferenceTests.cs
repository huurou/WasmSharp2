using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(WasmSpecVersion.Core2, 0, "malformed reference type", 11L)]
    [Arguments(WasmSpecVersion.Core3, 1, "zero byte expected", 12L)]
    [Arguments(WasmSpecVersion.Core3, 128, "zero byte expected", 12L)]
    public async Task テーブル初期化式の識別子が仕様版の構文に合わない_入力位置付きでデコードを拒否する(
        WasmSpecVersion version,
        int reserved,
        string diagnostic,
        long offset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(4, [1, 0x40, (byte)reserved, 0x70, 0, 1, 0xD0, 0x70, 0x0B])
        );

        // Act & Assert
        var exception = await Assert.That(() => WasmModule.Decode(binary, version)).ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, 4));
        }
    }

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
            .That(() => WasmModule.Decode(binary, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal)).IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, expressionOffset + relativeOffset, 0, 10));
        }
    }
}
