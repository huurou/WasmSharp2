using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments((byte)11)]
    [Arguments((byte)12)]
    public async Task Startと空のデータ関連セクションがある_両入口のDecodeとimport調査では実行せずInstantiateで実行する(
        byte sectionId
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "start", 0, [0])),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Section(sectionId, [0])
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
        using var stream = new MemoryStream(bytes);
        using var inspectionStream = new MemoryStream(bytes);

        // Act
        var inspections = new[]
        {
            WasmModule.InspectImports(bytes),
            WasmModule.InspectImports(inspectionStream),
        };
        var modules = new[]
        {
            WasmModule.Decode(bytes).Validate(),
            WasmModule.Decode(stream).Validate(),
        };

        // Assert
        await Assert.That(calls).IsEqualTo(0);
        foreach (var inspection in inspections)
        {
            using (Assert.Multiple())
            {
                await Assert.That(inspection.Imports.Single().Name).IsEqualTo("start");
                await Assert
                    .That(
                        inspection.UnverifiedRanges.Any(x =>
                            x.Stage == WasmProcessingStage.Decode
                            && x.StartOffset == bytes.Length - 1
                            && x.EndOffset == bytes.Length
                        )
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

        // Act
        foreach (var module in modules)
        {
            module.Instantiate([host]);
        }

        // Assert
        await Assert.That(calls).IsEqualTo(2);
    }

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
    [Arguments("0801000902FF", 14L, (byte)9)]
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
    public async Task Startと空要素sectionがある_両入力でDecodeとimport調査を実行してもStartを呼ばない()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(("env", "start", 0, [0])),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Section(9, [0])
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
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        using var inspectionStream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var fromBytes = WasmModule.Decode(bytes).Validate();
        var fromStream = WasmModule.Decode(stream).Validate();
        var inspection = WasmModule.InspectImports(bytes);
        var streamInspection = WasmModule.InspectImports(inspectionStream);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(calls).IsEqualTo(0);
            await Assert.That(inspection.Imports.Single().Name).IsEqualTo("start");
            await Assert.That(streamInspection.Imports.Single().Name).IsEqualTo("start");
            await Assert.That(fromBytes.ElementSegments.IsEmpty).IsTrue();
            await Assert.That(fromStream.ElementSegments.IsEmpty).IsTrue();
        }

        // Act
        fromBytes.Instantiate([host]);
        fromStream.Instantiate([host]);

        // Assert
        await Assert.That(calls).IsEqualTo(2);
    }
}
