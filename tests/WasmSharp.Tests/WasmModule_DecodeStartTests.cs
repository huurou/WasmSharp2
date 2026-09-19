using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Startの生の関数添字_両入力で実行や型検証をせず保持する(bool useStream)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Start(uint.MaxValue));
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Start!.Value.FunctionIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Start.Value.ByteOffset).IsEqualTo(10L);
            await Assert.That(module.Functions.IsEmpty).IsTrue();
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, 10, uint.MaxValue, 8));
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments("0800", 10L, (byte)8)]
    [Arguments("080180", 11L, (byte)8)]
    [Arguments("0805FFFFFFFF10", 14L, (byte)8)]
    [Arguments("08020000", 11L, (byte)8)]
    [Arguments("080100080100", 11L, (byte)8)]
    [Arguments("080100070100", 11L, (byte)7)]
    [Arguments("08020000090100", 11L, (byte)8)]
    [Arguments("0801000902FF", 13L, (byte)9)]
    public async Task Startや後続の外枠が破損_未対応に置き換えず両入力でDecode失敗になる(
        string hex,
        long offset,
        byte id
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(Convert.FromHexString(hex));
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        // Act & Assert
        foreach (var decode in decoders)
        {
            var exception = await Assert.That(decode).ThrowsExactly<WasmDecodeException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, id));
        }
    }

    [Test]
    [Arguments((byte)9, "section.element")]
    [Arguments((byte)11, "section.data")]
    [Arguments((byte)12, "section.data_count")]
    public async Task Startと未対応segmentがある_両入力でmoduleを返さず未確認範囲を示す(
        byte id,
        string feature
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Section(id, [0])
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        // Act & Assert
        foreach (var decode in decoders)
        {
            var exception = await Assert
                .That(decode)
                .ThrowsExactly<WasmUnsupportedFeatureException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Feature).IsEqualTo(feature);
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, 11, null, id));
                await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
                await Assert
                    .That(exception.UnverifiedRanges[0].Stage)
                    .IsEqualTo(WasmProcessingStage.Decode);
                await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(11L);
                await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(bytes.Length);
                await Assert
                    .That(exception.UnverifiedRanges[1].Stage)
                    .IsEqualTo(WasmProcessingStage.Validate);
                await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
                await Assert.That(exception.UnverifiedRanges[1].EndOffset).IsEqualTo(bytes.Length);
            }
        }
    }
}
