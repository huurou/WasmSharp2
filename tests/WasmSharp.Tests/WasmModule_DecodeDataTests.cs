using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task データセクションを読み元入力を変更する_両入口で初期バイト列とオフセット式を独立して保持する(
        bool useStream,
        bool explicitMemory
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Exports(("memory", 2, 0)),
            HostLinkingModuleBinary.Section(12, [2]),
            HostLinkingModuleBinary.Section(
                11,
                [
                    2,
                    .. explicitMemory ? new byte[] { 2, 0 } : [0],
                    0x41,
                    1,
                    0x0B,
                    2,
                    11,
                    22,
                    1,
                    1,
                    99,
                ]
            )
        );
        using var stream = new MemoryStream(bytes);

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        Array.Clear(bytes);
        var instance = module.Validate().Instantiate([]);
        var actual = new byte[4];
        instance.GetMemory("memory").Read(0, actual);

        // Assert
        await Assert.That(actual.SequenceEqual(new byte[] { 0, 11, 22, 0 })).IsTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("0B0100")]
    [Arguments("0C0100")]
    [Arguments("0C0280000B0100")]
    [Arguments("0B03010100")]
    [Arguments("0C01010B03010100")]
    public async Task データとデータ数セクションを省略するか整合する内容を指定する_メモリなしでも両入口で生成できる(
        string sections
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);
        foreach (var useStream in new[] { false, true })
        {
            using var stream = new MemoryStream(bytes);

            // Act
            var instance = (useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                .Validate()
                .Instantiate([]);

            // Assert
            await Assert.That(instance).IsNotNull();
        }
    }

    [Test]
    [Arguments("0C0101", (byte)12)]
    [Arguments("0C01010B0100", (byte)11)]
    [Arguments("0C01000B03010100", (byte)11)]
    [Arguments("0C05FFFFFFFF0F0B0100", (byte)11)]
    public async Task データ数が省略時の0件または宣言件数と一致しない_両入口で読了後にDecode失敗になる(
        string sections,
        byte sectionId
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);
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
                            "data count and data section have inconsistent lengths",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length, null, sectionId));
            }
        }
    }

    [Test]
    [Arguments("0B020103", "malformed data segment kind", 11L, (byte)11)]
    [Arguments("0B0701808080808000", "integer representation too long", 16L, (byte)11)]
    [Arguments("0B07010041FFFFFFFF1F", "integer too large", 17L, (byte)11)]
    [Arguments("0B0401004100", "unexpected end", 14L, (byte)11)]
    [Arguments("0B0401000B80", "unexpected end", 14L, (byte)11)]
    [Arguments("0B04010102AA", "unexpected end", 13L, (byte)11)]
    [Arguments("0B020000", "section size mismatch", 11L, (byte)11)]
    [Arguments("0C0180", "unexpected end", 11L, (byte)12)]
    [Arguments("0C05FFFFFFFF10", "integer too large", 14L, (byte)12)]
    [Arguments("0C06808080808000", "integer representation too long", 15L, (byte)12)]
    [Arguments("0C020000", "section size mismatch", 11L, (byte)12)]
    [Arguments("0C01000C0100", "unexpected content after last section", 11L, (byte)12)]
    [Arguments("0B01000B0100", "unexpected content after last section", 11L, (byte)11)]
    [Arguments("0B01000C0100", "unexpected content after last section", 11L, (byte)12)]
    [Arguments("0A01000C0100", "unexpected content after last section", 11L, (byte)12)]
    public async Task データとデータ数の構文や順序が不正である_両入口でDecode診断と位置を返す(
        string sections,
        string diagnostic,
        long offset,
        byte sectionId
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);
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
                    .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, sectionId));
            }
        }
    }

    [Test]
    [Arguments("0103", "malformed data segment kind", 1)]
    [Arguments("0100FF", "illegal opcode", 1)]
    [Arguments("01004100", "unexpected end", 0)]
    [Arguments("01010A11", "length out of bounds", 2)]
    [Arguments("0000", "section size mismatch", 1)]
    public async Task 関数件数とデータ件数が不一致でデータ構文も不正である_件数照合より先に構文違反を返す(
        string data,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(12, [2]),
            HostLinkingModuleBinary.Section(11, Convert.FromHexString(data))
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
                .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - offsetFromEnd, null, 11));
        }
    }

    [Test]
    [Arguments(true, 1, "function and code section have inconsistent lengths")]
    [Arguments(true, 2, "function and code section have inconsistent lengths")]
    [Arguments(false, 2, "data count and data section have inconsistent lengths")]
    public async Task データの式が未対応で宣言件数の違反が確定している_関数件数とデータ件数の順でDecode失敗を選ぶ(
        bool functionMismatch,
        byte dataCount,
        string diagnostic
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            functionMismatch ? HostLinkingModuleBinary.Functions(0) : [],
            HostLinkingModuleBinary.Section(12, [dataCount]),
            HostLinkingModuleBinary.Section(11, [1, 0, 0x11, 0xFF]),
            [13, 0]
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
                .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - 3, null, 11));
        }
    }

    [Test]
    [Arguments(false, "010011FF", 1)]
    [Arguments(true, "0201000011FF", 2)]
    public async Task データの式が未対応で確定済みの件数違反がない_後続を調べず原因と未確認範囲を保持する(
        bool hasDataCount,
        string data,
        byte count
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            hasDataCount ? HostLinkingModuleBinary.Section(12, [count]) : [],
            HostLinkingModuleBinary.Section(11, Convert.FromHexString(data)),
            [13, 0]
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo("call_indirect");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - 4, null, 11));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Decode);
            await Assert
                .That(exception.UnverifiedRanges[0].StartOffset)
                .IsEqualTo(bytes.Length - 4L);
            await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(bytes.Length);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
            await Assert.That(exception.UnverifiedRanges[1].EndOffset).IsEqualTo(bytes.Length);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task データの宣言範囲外に未対応命令がある_診断再走査でも元の境界違反を件数不一致で置き換えない(
        bool functionMismatch
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            functionMismatch ? HostLinkingModuleBinary.Functions(0) : [],
            HostLinkingModuleBinary.Section(12, [2]),
            HostLinkingModuleBinary.Section(11, [1, 0, 0x41, 0]),
            HostLinkingModuleBinary.Section(0, [0x11])
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("unexpected end", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - 3, null, 11));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task コードが関数宣言より少ないか多く後続データも破損している_両入口でデータ構文の違反を先に報告する(
        bool extraBody
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            extraBody ? [] : HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(12, [2]),
            extraBody ? HostLinkingModuleBinary.Code(([], [0x0B])) : HostLinkingModuleBinary.Code(),
            HostLinkingModuleBinary.Section(11, [1, 3])
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
                            "malformed data segment kind",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length - 1, null, 11));
            }
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task コード内未対応命令で読了できない_確定した関数件数だけを照合して後続データの件数を推測しない(
        bool functionMismatch
    )
    {
        // Arrange
        var data = HostLinkingModuleBinary.Section(11, [1, 1, 0]);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            functionMismatch ? [] : HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(([], [0x11, 0xFF])),
            data,
            [13, 0]
        );
        foreach (var useStream in new[] { false, true })
        {
            using var stream = new MemoryStream(bytes);

            // Act & Assert
            if (functionMismatch)
            {
                var exception = await Assert
                    .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                    .ThrowsExactly<WasmDecodeException>();
                using (Assert.Multiple())
                {
                    await Assert
                        .That(
                            exception!.Message.StartsWith(
                                "function and code section have inconsistent lengths",
                                StringComparison.Ordinal
                            )
                        )
                        .IsTrue();
                    await Assert
                        .That(exception.Location)
                        .IsEqualTo(
                            new(
                                WasmProcessingStage.Decode,
                                bytes.Length - data.Length - 2,
                                null,
                                10
                            )
                        );
                }
            }
            else
            {
                var exception = await Assert
                    .That(() => useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes))
                    .ThrowsExactly<WasmUnsupportedFeatureException>();
                using (Assert.Multiple())
                {
                    await Assert.That(exception!.Feature).IsEqualTo("call_indirect");
                    await Assert
                        .That(exception.Location)
                        .IsEqualTo(
                            new(WasmProcessingStage.Decode, bytes.Length - data.Length - 4, 0, 10)
                        );
                    await Assert
                        .That(exception.UnverifiedRanges[0].EndOffset)
                        .IsEqualTo(bytes.Length);
                }
            }
        }
    }
}
