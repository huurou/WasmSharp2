using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Global初期化式の定数とglobal取得_両入力で型とビット列と生添字を未評価で保持する(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals(
                (0x7F, false, [0x41, 0x7F, 0x0B]),
                (0x7E, true, [0x42, 0x7F, 0x0B]),
                (0x7D, false, [0x43, 0x34, 0x12, 0xC0, 0x7F, 0x0B]),
                (0x7C, true, [0x44, 0, 0, 0, 0, 0, 0, 0, 0x80, 0x0B]),
                (0x7B, false, [0x23, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x0B]),
                (0x70, false, [0x23, 0, 0x0B]),
                (0x6F, true, [0x23, 1, 0x0B])
            )
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var module = useStream ? WasmModule.Decode(stream) : WasmModule.Decode(bytes);
        Array.Clear(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Globals.Length).IsEqualTo(7);
            await Assert
                .That(module.Globals[0].Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.I32, false));
            await Assert.That(module.Globals[0].ByteOffset).IsEqualTo(11L);
            await Assert.That(module.Globals[0].Initializer.Length).IsEqualTo(2);
            await Assert.That(module.Globals[0].Initializer[0].ByteOffset).IsEqualTo(13L);
            await Assert.That(module.Globals[0].Initializer[0].Immediate.AsI32()).IsEqualTo(-1);
            await Assert
                .That(module.Globals[0].Initializer[1].Opcode)
                .IsEqualTo(new OpcodeKey(0, 0x0B));
            await Assert
                .That(module.Globals[1].Type)
                .IsEqualTo(new WasmGlobalType(WasmValueKind.I64, true));
            await Assert.That(module.Globals[1].Initializer[0].Immediate.AsI64()).IsEqualTo(-1L);
            await Assert
                .That(module.Globals[2].Initializer[0].Immediate.AsF32Bits())
                .IsEqualTo(0x7FC01234u);
            await Assert
                .That(module.Globals[3].Initializer[0].Immediate.AsF64Bits())
                .IsEqualTo(0x8000000000000000UL);
            await Assert.That(module.Globals[4].Type.ValueKind).IsEqualTo(WasmValueKind.V128);
            await Assert
                .That(module.Globals[4].Initializer[0].Opcode)
                .IsEqualTo(new OpcodeKey(0, 0x23));
            await Assert.That(module.Globals[4].Initializer[0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Globals[5].Type.ValueKind).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(module.Globals[5].Initializer[0].Index).IsEqualTo(0u);
            await Assert.That(module.Globals[6].Type.ValueKind).IsEqualTo(WasmValueKind.ExternRef);
            await Assert.That(module.Globals[6].Initializer[0].Index).IsEqualTo(1u);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments("0B", 1)]
    [Arguments("410041010B", 3)]
    [Arguments("42000B", 2)]
    public async Task Global式の結果数や型が未検証_構文として保持して検証段階へ渡す(
        string hex,
        int count
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((0x7F, false, Convert.FromHexString(hex)))
        );

        // Act
        var module = WasmModule.Decode(bytes);

        // Assert
        await Assert.That(module.Globals[0].Initializer.Length).IsEqualTo(count);
        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
        await Assert.That(() => module.Instantiate([])).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments("016E000B", 11L)]
    [Arguments("017F020B", 12L)]
    [Arguments("017F004100", 15L)]
    [Arguments("017F004180", 15L)]
    [Arguments("017F00430000", 14L)]
    [Arguments("017F002380", 15L)]
    [Arguments("017F0023FFFFFFFF100B", 18L)]
    [Arguments("017F00FF0B", 13L)]
    [Arguments("017F00050B", 13L)]
    [Arguments("017F000B00", 14L)]
    [Arguments("027F0041000B", 16L)]
    public async Task Global定義や式の構文が破損_両入力で元位置付きDecode失敗になる(
        string hex,
        long offset
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(6, Convert.FromHexString(hex))
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
            var exception = await Assert.That(decode).ThrowsExactly<WasmDecodeException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, 6));
        }
    }

    [Test]
    [Arguments("D0700B", "ref.null")]
    [Arguments("FD0C", "v128.const")]
    public async Task Global初期化式が未対応命令_機能と位置と未確認範囲を返す(
        string hex,
        string feature
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((0x70, false, Convert.FromHexString(hex)))
        );

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 13, null, 6));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(13L);
            await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(bytes.Length);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
        }
    }
}
