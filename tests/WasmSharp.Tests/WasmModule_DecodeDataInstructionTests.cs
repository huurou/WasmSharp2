using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments("FC08", "unexpected end", 0)]
    [Arguments("FC0880", "unexpected end", 0)]
    [Arguments("FC0800", "unexpected end", 0)]
    [Arguments("FC0800010B", "zero byte expected", 2)]
    [Arguments("FC080080000B", "zero byte expected", 3)]
    [Arguments("FC08808080808000", "integer representation too long", 1)]
    [Arguments("FC08FFFFFFFF1F00", "integer too large", 2)]
    [Arguments("FC09", "unexpected end", 0)]
    [Arguments("FC0980", "unexpected end", 0)]
    [Arguments("FC09808080808000", "integer representation too long", 1)]
    [Arguments("FC09FFFFFFFF1F", "integer too large", 1)]
    public async Task データ参照命令の即値が不正か欠落する_両Decode入口が構文診断と元位置を返す(
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        foreach (var useStream in new[] { false, true })
        {
            using var stream = new MemoryStream(bytes);

            // Act & Assert
            var exception = await Assert
                .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                .ThrowsExactly<WasmDecodeException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new(WasmProcessingStage.Decode, bytes.Length - offsetFromEnd, 0, 10)
                    );
            }
        }
    }

    [Test]
    [Arguments("FC0800000B")]
    [Arguments("00FC0800000B")]
    [Arguments("FC09000B")]
    [Arguments("00FC09000B")]
    public async Task 到達性によらずコードがデータインデックスを使いデータ数宣言がない_両Decode入口が欠落を拒否する(
        string instructions
    )
    {
        // Arrange
        foreach (var hasData in new[] { false, true })
        {
            var bytes = HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions))),
                hasData ? HostLinkingModuleBinary.Section(11, [1, 1, 0]) : []
            );
            foreach (var useStream in new[] { false, true })
            {
                using var stream = new MemoryStream(bytes);

                // Act & Assert
                var exception = await Assert
                    .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                    .ThrowsExactly<WasmDecodeException>();
                using (Assert.Multiple())
                {
                    await Assert
                        .That(
                            exception!.Message.StartsWith(
                                "data count section required",
                                StringComparison.Ordinal
                            )
                        )
                        .IsTrue();
                    await Assert
                        .That(exception.Location)
                        .IsEqualTo(
                            new(
                                WasmProcessingStage.Decode,
                                bytes.Length,
                                null,
                                hasData ? (byte)11 : (byte)10
                            )
                        );
                }
            }
        }
    }

    [Test]
    [Arguments(true, (byte)2, "010100", "function and code section have inconsistent lengths")]
    [Arguments(false, (byte)2, "010100", "data count and data section have inconsistent lengths")]
    [Arguments(true, (byte)2, "0103", "malformed data segment kind")]
    [Arguments(false, null, "0103", "malformed data segment kind")]
    public async Task データ参照命令と件数違反や構文違反が併存する_構文と関数件数とデータ件数の順でDecode失敗を選ぶ(
        bool functionMismatch,
        byte? dataCount,
        string data,
        string diagnostic
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            functionMismatch
                ? HostLinkingModuleBinary.Functions(0, 0)
                : HostLinkingModuleBinary.Functions(0),
            dataCount is { } count ? HostLinkingModuleBinary.Section(12, [count]) : [],
            HostLinkingModuleBinary.Code(([], [0xFC, 9, 0, 0x0B])),
            HostLinkingModuleBinary.Section(11, Convert.FromHexString(data))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
            .IsTrue();
    }

    [Test]
    [Arguments(true, (byte)2, "function and code section have inconsistent lengths")]
    [Arguments(false, (byte)2, "data count and data section have inconsistent lengths")]
    [Arguments(false, null, "data count section required")]
    public async Task データ参照後に完全なメモリ式がある_関数件数とデータ件数とデータ数宣言の順で検査する(
        bool functionMismatch,
        byte? dataCount,
        string diagnostic
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            functionMismatch
                ? HostLinkingModuleBinary.Functions(0, 0)
                : HostLinkingModuleBinary.Functions(0),
            dataCount is { } count ? HostLinkingModuleBinary.Section(12, [count]) : [],
            HostLinkingModuleBinary.Code(([], [0xFC, 9, 0, 0x0B])),
            HostLinkingModuleBinary.Section(11, [1, 0, 0xFD, 0, 0, 0, 0x0B, 0])
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length, null, 11));
        }
    }
}
