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
    public async Task Startと未対応segmentがある_両入力でimportを調査できるがstartは実行しない(
        byte id,
        string feature
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "start", 0, [0])),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Section(id, [0])
        );
        var calls = 0;
        var host = new WasmHostModule("env");
        host.Define(
            "start",
            WasmFunction.CreateHost(
                new([], []),
                _ =>
                {
                    calls++;
                    return new([]);
                }
            )
        );
        var segmentOffset = bytes.Length - 3;
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        using var inspectionStream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmModule>[] decoders =
        [
            () => WasmModule.Decode(bytes),
            () => WasmModule.Decode(stream),
        ];

        // Act
        var fromBytes = WasmModule.InspectImports(bytes);
        var fromStream = WasmModule.InspectImports(inspectionStream);

        // Act & Assert
        foreach (var decode in decoders)
        {
            var exception = await Assert
                .That(() => decode().Validate().Instantiate([host]))
                .ThrowsExactly<WasmUnsupportedFeatureException>();
            using (Assert.Multiple())
            {
                await Assert.That(exception!.Feature).IsEqualTo(feature);
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, segmentOffset, null, id));
                await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
                await Assert
                    .That(exception.UnverifiedRanges[0].Stage)
                    .IsEqualTo(WasmProcessingStage.Decode);
                await Assert
                    .That(exception.UnverifiedRanges[0].StartOffset)
                    .IsEqualTo(segmentOffset);
                await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(bytes.Length);
                await Assert
                    .That(exception.UnverifiedRanges[1].Stage)
                    .IsEqualTo(WasmProcessingStage.Validate);
                await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
                await Assert.That(exception.UnverifiedRanges[1].EndOffset).IsEqualTo(bytes.Length);
            }
        }
        foreach (var inspection in new[] { fromBytes, fromStream })
        {
            using (Assert.Multiple())
            {
                await Assert.That(inspection.Imports.Single().ModuleName).IsEqualTo("env");
                await Assert.That(inspection.Imports.Single().Name).IsEqualTo("start");
                await Assert
                    .That(
                        inspection.UnverifiedRanges.Any(x => x.Stage == WasmProcessingStage.Decode)
                    )
                    .IsTrue();
                await Assert
                    .That(
                        inspection.UnverifiedRanges.Any(x =>
                            x.Stage == WasmProcessingStage.Validate
                        )
                    )
                    .IsTrue();
            }
        }
        await Assert.That(calls).IsEqualTo(0);

        // Act
        WasmModule.Decode(bytes.AsSpan(0, segmentOffset)).Validate().Instantiate([host]);

        // Assert
        await Assert.That(calls).IsEqualTo(1);
    }
}
