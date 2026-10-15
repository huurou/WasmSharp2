using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ExpressionDecoder_ReadTests
{
    [Test]
    [Arguments("40", BlockTypeKind.Empty, WasmValueKind.I32, 0u)]
    [Arguments("7F", BlockTypeKind.Value, WasmValueKind.I32, 0u)]
    [Arguments("7E", BlockTypeKind.Value, WasmValueKind.I64, 0u)]
    [Arguments("7D", BlockTypeKind.Value, WasmValueKind.F32, 0u)]
    [Arguments("7C", BlockTypeKind.Value, WasmValueKind.F64, 0u)]
    [Arguments("7B", BlockTypeKind.Value, WasmValueKind.V128, 0u)]
    [Arguments("70", BlockTypeKind.Value, WasmValueKind.FuncRef, 0u)]
    [Arguments("6F", BlockTypeKind.Value, WasmValueKind.ExternRef, 0u)]
    [Arguments("00", BlockTypeKind.TypeIndex, WasmValueKind.I32, 0u)]
    [Arguments("8000", BlockTypeKind.TypeIndex, WasmValueKind.I32, 0u)]
    [Arguments("8100", BlockTypeKind.TypeIndex, WasmValueKind.I32, 1u)]
    [Arguments("FFFFFFFF0F", BlockTypeKind.TypeIndex, WasmValueKind.I32, uint.MaxValue)]
    public async Task 制御型が空か値型か非負s33である_形式と値を保持して型添字を検証しない(
        string type,
        BlockTypeKind kind,
        WasmValueKind valueType,
        uint typeIndex
    )
    {
        // Arrange
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            foreach (var code in new byte[] { 0x02, 0x03, 0x04 })
            {
                var bytes = Convert.FromHexString(code.ToString("X2") + type + "0B0BFF");
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);

                // Act
                var instructions = ExpressionDecoder.Read(ref reader, 100);
                var position = reader.Position;
                var followingByte = reader.ReadByte();

                // Assert
                using (Assert.Multiple())
                {
                    await Assert.That(instructions.Count).IsEqualTo(3);
                    await Assert.That(instructions[0].Opcode).IsEqualTo(new OpcodeKey(0, code));
                    await Assert.That(instructions[0].BlockType.Kind).IsEqualTo(kind);
                    await Assert.That(instructions[0].BlockType.ValueType).IsEqualTo(valueType);
                    await Assert.That(instructions[0].BlockType.TypeIndex).IsEqualTo(typeIndex);
                    await Assert.That(instructions[0].ByteOffset).IsEqualTo(40L);
                    await Assert.That(position).IsEqualTo(40L + bytes.Length - 1);
                    await Assert.That(followingByte).IsEqualTo((byte)0xFF);
                }
            }
        }
    }

    [Test]
    [Arguments("024003400440010502400B0B0B0B0B", "0203040105020B0B0B0B0B")]
    [Arguments("044004400B0B0B", "04040B0B0B")]
    public async Task 制御が入れ子になりelseを省略できる_内側endで止まらず最外endまで保持する(
        string hex,
        string expectedOpcodes
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex + "FF");
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);

            // Act
            var instructions = ExpressionDecoder.Read(ref reader, 100);
            var position = reader.Position;
            var remaining = reader.Remaining;

            // Assert
            using (Assert.Multiple())
            {
                await Assert
                    .That(Convert.ToHexString([.. instructions.Select(x => (byte)x.Opcode.Code)]))
                    .IsEqualTo(expectedOpcodes);
                await Assert.That(instructions[^1].ByteOffset).IsEqualTo(position - 1);
                await Assert.That(position).IsEqualTo(40L + bytes.Length - 1);
                await Assert.That(remaining).IsEqualTo(1);
            }
        }
    }

    [Test]
    public async Task 分岐添字と空を含む分岐表がある_非最短u32と候補順と末尾の既定を保持する()
    {
        // Arrange
        var bytes = Convert.FromHexString(
            "0CFFFFFFFF0F0D80000E038000FFFFFFFF0F02FFFFFFFF0F0E00FFFFFFFF0F0B"
        );
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);

            // Act
            var instructions = ExpressionDecoder.Read(ref reader, 100);
            var remaining = reader.Remaining;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(instructions.Count).IsEqualTo(5);
                await Assert.That(instructions[0].Index).IsEqualTo(uint.MaxValue);
                await Assert.That(instructions[1].Index).IsEqualTo(0u);
                await Assert
                    .That(
                        instructions[2]
                            .Labels.SequenceEqual(new uint[] { 0, uint.MaxValue, 2, uint.MaxValue })
                    )
                    .IsTrue();
                await Assert.That(instructions[3].Labels.Length).IsEqualTo(1);
                await Assert.That(instructions[3].Labels[0]).IsEqualTo(uint.MaxValue);
                await Assert.That(instructions[2].ByteOffset).IsEqualTo(49L);
                await Assert.That(remaining).IsEqualTo(0);
            }
        }
    }

    [Test]
    [Arguments("00", "")]
    [Arguments("017F", "I32")]
    [Arguments("087F7E7D7C7B706F7F", "I32,I64,F32,F64,V128,FuncRef,ExternRef,I32")]
    [Arguments("81007E", "I64")]
    public async Task Select型列が空か単一か複数である_値型を保持して長さを検証しない(
        string vector,
        string expectedTypes
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("1B1C" + vector + "0B");
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);

            // Act
            var instructions = ExpressionDecoder.Read(ref reader, 100);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(instructions.Count).IsEqualTo(3);
                await Assert.That(instructions[0].Opcode).IsEqualTo(new OpcodeKey(0, 0x1B));
                await Assert.That(instructions[0].SelectTypes.IsEmpty).IsTrue();
                await Assert.That(instructions[1].Opcode).IsEqualTo(new OpcodeKey(0, 0x1C));
                await Assert
                    .That(string.Join(',', instructions[1].SelectTypes))
                    .IsEqualTo(expectedTypes);
                await Assert.That(instructions[1].SelectTypes.IsDefault).IsFalse();
            }
        }
    }

    [Test]
    [Arguments("02400528", 42L, "END opcode expected")]
    [Arguments("03400528", 42L, "END opcode expected")]
    [Arguments("0440050528", 43L, "END opcode expected")]
    [Arguments("02400B0528", 43L, "END opcode expected")]
    [Arguments("026E0B0B", 41L, "malformed reference type")]
    [Arguments("02FF7F0B0B", 42L, "integer representation too long")]
    [Arguments("02C07F0B0B", 42L, "integer representation too long")]
    [Arguments("1C016E0B", 42L, "malformed reference type")]
    [Arguments("1C01FF7F0B", 43L, "integer representation too long")]
    [Arguments("0EFFFFFFFF0F00", 41L, "length out of bounds")]
    [Arguments("1CFFFFFFFF0F7F", 41L, "length out of bounds")]
    public async Task 制御配置か複合即値が不正である_後続未対応より構文診断と位置を優先する(
        string hex,
        long offset,
        string prefix
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
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
                    .IsEqualTo(new(WasmProcessingStage.Decode, offset, 3, 10));
            }
        }
    }

    [Test]
    [Arguments("024003400B", 45L)]
    [Arguments("044005", 43L)]
    [Arguments("0280", 42L)]
    [Arguments("0E01", 42L)]
    [Arguments("0E0100", 43L)]
    [Arguments("1C01", 42L)]
    [Arguments("1C027F", 43L)]
    public async Task 制御終端か複合即値が切れる_通常解析と診断走査で元の境界位置を保持する(
        string hex,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            // Act & Assert
            Action read = () =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 10, 3, mode);
                ExpressionDecoder.Read(ref reader, 100);
            };
            WasmDecodeException failure;
            if (mode == ModuleReadMode.Bounded)
            {
                var exception = await Assert
                    .That(read)
                    .ThrowsExactly<ModuleReadBoundaryException>();
                failure = exception!.Fallback;
            }
            else
            {
                failure = (await Assert.That(read).ThrowsExactly<WasmDecodeException>())!;
            }
            using (Assert.Multiple())
            {
                await Assert
                    .That(
                        failure.Message.StartsWith(
                            "unexpected end of section or function",
                            StringComparison.Ordinal
                        )
                    )
                    .IsTrue();
                await Assert
                    .That(failure.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, offset, 3, 10));
            }
        }
    }

    [Test]
    public async Task 深いblockの入れ子がある_CLR再帰を増やさず最外endまで読む()
    {
        // Arrange
        const int DEPTH = 20_000;
        var bytes = Enumerable
            .Repeat(new byte[] { 0x02, 0x40 }, DEPTH)
            .SelectMany(x => x)
            .Concat(Enumerable.Repeat((byte)0x0B, DEPTH + 1))
            .ToArray();
        var reader = new ModuleBinaryReader(bytes);

        // Act
        var instructions = ExpressionDecoder.Read(ref reader, bytes.Length);
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(instructions.Count).IsEqualTo(DEPTH * 2 + 1);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

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
                        .SequenceEqual([40, 42, 44, 49, 58, 61, 62, 63])
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
    [Arguments(ModuleReadMode.Bounded, "25", "table.get")]
    [Arguments(ModuleReadMode.Diagnostic, "25", "table.get")]
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
