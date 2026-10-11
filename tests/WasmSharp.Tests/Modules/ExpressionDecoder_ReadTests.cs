using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ExpressionDecoder_ReadTests
{
    [Test]
    [Arguments(ModuleReadMode.Bounded)]
    [Arguments(ModuleReadMode.Diagnostic)]
    public async Task 対応命令が続く_即値と入力順と位置を保持してend直後で止まる(
        ModuleReadMode mode
    )
    {
        // Arrange
        var reader = new ModuleBinaryReader(
            Convert.FromHexString("417F427E434523C1FF44BC9A78563412F8FF2080011A0F0BFF"),
            40,
            10,
            3,
            mode
        );

        // Act
        var instructions = ExpressionDecoder.Read(ref reader, 100);
        var position = reader.Position;
        var remaining = reader.Remaining;
        var followingByte = reader.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instructions.Count).IsEqualTo(8);
            await Assert
                .That(
                    instructions
                        .Select(x => x.Opcode)
                        .SequenceEqual(
                            new byte[] { 0x41, 0x42, 0x43, 0x44, 0x20, 0x1A, 0x0F, 0x0B }.Select(
                                x => new OpcodeKey(0, x)
                            )
                        )
                )
                .IsTrue();
            await Assert
                .That(
                    instructions
                        .Select(x => x.ByteOffset)
                        .SequenceEqual(new long[] { 40, 42, 44, 49, 58, 61, 62, 63 })
                )
                .IsTrue();
            await Assert.That(instructions[0].Immediate.AsI32()).IsEqualTo(-1);
            await Assert.That(instructions[1].Immediate.AsI64()).IsEqualTo(-2L);
            await Assert.That(instructions[2].Immediate.AsF32Bits()).IsEqualTo(0xFFC12345u);
            await Assert
                .That(instructions[3].Immediate.AsF64Bits())
                .IsEqualTo(0xFFF8123456789ABCUL);
            await Assert.That(instructions[4].Index).IsEqualTo(128u);
            await Assert.That(position).IsEqualTo(64L);
            await Assert.That(remaining).IsEqualTo(1);
            await Assert.That(followingByte).IsEqualTo((byte)0xFF);
        }
    }

    [Test]
    public async Task 空の式_終端命令を保持して次の式を読まない()
    {
        // Arrange
        var reader = new ModuleBinaryReader([0x0B, 0x41, 0x00, 0x0B], 40, 6);

        // Act
        var instructions = ExpressionDecoder.Read(ref reader, 100);
        var position = reader.Position;
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instructions.Count).IsEqualTo(1);
            await Assert.That(instructions[0].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
            await Assert.That(instructions[0].ByteOffset).IsEqualTo(40L);
            await Assert.That(position).IsEqualTo(41L);
            await Assert.That(remaining).IsEqualTo(3);
        }
    }

    [Test]
    [Arguments("4100", 42L)]
    [Arguments("4180", 42L)]
    [Arguments("43AABBCC", 41L)]
    public async Task 通常解析で終端か即値が切れる_元の位置と境界診断を返す(string hex, long offset)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3);
                ExpressionDecoder.Read(ref reader, 100);
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Fallback.Message.StartsWith(
                        "unexpected end of section or function",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Fallback.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 3, 10));
        }
    }

    [Test]
    public async Task 診断走査で物理終端に達する_内部境界通知を出さず同じ位置のDecode失敗になる()
    {
        // Arrange
        var bytes = Convert.FromHexString("4180");

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3, ModuleReadMode.Diagnostic);
                ExpressionDecoder.Read(ref reader, 100);
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "unexpected end of section or function",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 42, 3, 10));
        }
    }

    [Test]
    public async Task 宣言範囲の後にendがある_通常解析は境界不正として後続を読まない()
    {
        // Arrange
        var bytes = Convert.FromHexString("41000BFF");

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3);
                var expression = reader.ReadRange(2);
                ExpressionDecoder.Read(ref expression, 100);
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, 42, 3, 10));
    }

    [Test]
    public async Task 宣言範囲の後にendがある_診断走査は同じ構文処理で物理範囲を読む()
    {
        // Arrange
        var reader = new ModuleBinaryReader(
            Convert.FromHexString("41000BFF"),
            40,
            10,
            3,
            ModuleReadMode.Diagnostic
        );
        var expression = reader.ReadRange(2);

        // Act
        var instructions = ExpressionDecoder.Read(ref expression, 100);
        var position = expression.Position;
        var declaredEnd = expression.DeclaredEnd;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instructions.Count).IsEqualTo(2);
            await Assert.That(instructions[1].ByteOffset).IsEqualTo(42L);
            await Assert.That(position).IsEqualTo(43L);
            await Assert.That(declaredEnd).IsEqualTo(42L);
        }
    }

    [Test]
    [Arguments(ModuleReadMode.Bounded, "FC8000", "i32.trunc_sat_f32_s")]
    [Arguments(ModuleReadMode.Diagnostic, "FC8000", "i32.trunc_sat_f32_s")]
    [Arguments(ModuleReadMode.Bounded, "FD0C", "v128.const")]
    public async Task 未対応命令の後に不正がある_後続を調べず機能と位置と未確認範囲を保持する(
        ModuleReadMode mode,
        string opcode,
        string feature
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("417F" + opcode + "FF05");

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);
                ExpressionDecoder.Read(ref reader, 100);
            })
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 42, 3, 10));
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(2);
            await Assert
                .That(exception.UnverifiedRanges[0])
                .IsEqualTo(
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Decode,
                        42,
                        100,
                        "この構文以降のデコードが未完了です。"
                    )
                );
            await Assert
                .That(exception.UnverifiedRanges[1])
                .IsEqualTo(
                    new WasmUnverifiedRange(
                        WasmProcessingStage.Validate,
                        0,
                        100,
                        "入力全体の検証が未実施です。"
                    )
                );
        }
    }

    [Test]
    [Arguments(ModuleReadMode.Bounded, "FC12", "illegal opcode fc 12")]
    [Arguments(ModuleReadMode.Diagnostic, "FC12", "illegal opcode fc 12")]
    [Arguments(ModuleReadMode.Bounded, "056A", "END opcode expected")]
    [Arguments(ModuleReadMode.Diagnostic, "056A", "END opcode expected")]
    public async Task 未割当命令か対応しないelseがある_後続未対応より構文不正を優先する(
        ModuleReadMode mode,
        string hex,
        string prefix
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);
                ExpressionDecoder.Read(ref reader, 100);
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 40, 3, 10));
        }
    }
}
