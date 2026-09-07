using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleDecoder_DecodeTests
{
    [Test]
    [Arguments("020100", (byte)2, "section.import")]
    [Arguments("040100", (byte)4, "section.table")]
    [Arguments("050100", (byte)5, "section.memory")]
    [Arguments("060100", (byte)6, "section.global")]
    [Arguments("080100", (byte)8, "section.start")]
    [Arguments("090100", (byte)9, "section.element")]
    [Arguments("0B0100", (byte)11, "section.data")]
    [Arguments("0C0100", (byte)12, "section.data_count")]
    public async Task 未対応sectionの後に破損がある_機能と未確認範囲を返して中断する(
        string section,
        byte id,
        string feature
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + section + "0D00");

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 8, null, id));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(8L);
            await Assert
                .That(exception.UnverifiedRanges[0].EndOffset)
                .IsEqualTo((long)bytes.Length);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
            await Assert
                .That(exception.UnverifiedRanges[1].EndOffset)
                .IsEqualTo((long)bytes.Length);
        }
    }

    [Test]
    [Arguments("6AFF05", "i32.add")]
    [Arguments("02", "block")]
    [Arguments("1080", "call")]
    [Arguments("FC00", "i32.trunc_sat_f32_s")]
    [Arguments("FC8000", "i32.trunc_sat_f32_s")]
    [Arguments("FC0A", "memory.copy")]
    [Arguments("FD00", "v128.load")]
    [Arguments("FD0C", "v128.const")]
    [Arguments("FDDF01", "i64x2.extmul_high_i32x4_u")]
    public async Task 割当済み未対応命令_即値や後続を調べず未確認範囲を返す(
        string instructions,
        string feature
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 33, 0, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(33L);
            await Assert
                .That(exception.UnverifiedRanges[0].EndOffset)
                .IsEqualTo((long)bytes.Length);
            await Assert
                .That(exception.UnverifiedRanges[1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(0L);
            await Assert
                .That(exception.UnverifiedRanges[1].EndOffset)
                .IsEqualTo((long)bytes.Length);
        }
    }

    [Test]
    [Arguments("FF", 33L)]
    [Arguments("D3", 33L)]
    [Arguments("FC12", 33L)]
    [Arguments("FCFFFFFFFF0F", 33L)]
    [Arguments("FD9A01", 33L)]
    [Arguments("FD8002", 33L)]
    [Arguments("FDFFFFFFFF0F", 33L)]
    [Arguments("FC", 34L)]
    [Arguments("FD80", 35L)]
    [Arguments("FCFFFFFFFF10", 38L)]
    [Arguments("FD808080808000", 38L)]
    [Arguments("056A", 33L)]
    public async Task 未割当opcodeか不正LEBか平坦else_未対応に置き換えず破損になる(
        string instructions,
        long offset
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, 0, 10));
    }

    [Test]
    [Arguments((byte)1, "export.table")]
    [Arguments((byte)2, "export.memory")]
    [Arguments((byte)3, "export.global")]
    public async Task 関数以外の既知export_種類を機能名にして中断する(byte kind, string feature)
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D0100000007030100" + kind.ToString("X2"));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 12, null, 7));
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(12L);
            await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(13L);
        }
    }

    [Test]
    [Arguments("0202FF", 10L, (byte)2)]
    [Arguments("070100020100", 11L, (byte)2)]
    [Arguments("0A01000C0100", 11L, (byte)12)]
    [Arguments("030201000B0100", 12L, (byte)11)]
    [Arguments("03020100000201610B0100", 16L, (byte)11)]
    public async Task 未対応内容より前に長さや順序や件数の違反が確定する_破損を優先する(
        string sections,
        long offset,
        byte id
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, id));
    }
}
