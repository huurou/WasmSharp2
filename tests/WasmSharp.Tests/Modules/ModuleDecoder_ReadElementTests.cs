using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ModuleDecoder_ReadElementTests
{
    [Test]
    [Arguments("0041040B", ElementMode.Active, 0u)]
    [Arguments("0100", ElementMode.Passive, 0u)]
    [Arguments("02FFFFFFFF0F41040B00", ElementMode.Active, uint.MaxValue)]
    [Arguments("0300", ElementMode.Declarative, 0u)]
    public async Task 旧形式の関数添字列がある_元の位置を持つ参照式へ正規化する(
        string prefix,
        ElementMode mode,
        uint tableIndex
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("01" + prefix + "028000FFFFFFFF0FFF");
        var firstIndexOffset = 42L + prefix.Length / 2;
        var reader = new ModuleBinaryReader(bytes, 40, 9);

        // Act
        var segments = ModuleDecoder.ReadElement(ref reader, 100);
        var followingByte = reader.ReadByte();
        Array.Clear(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(1);
            var segment = segments[0];
            await Assert.That(segment.Mode).IsEqualTo(mode);
            await Assert.That(segment.ElementType).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(segment.ByteOffset).IsEqualTo(41L);
            await Assert.That(segment.Initializers.Length).IsEqualTo(2);
            for (var index = 0; index < segment.Initializers.Length; index++)
            {
                var expression = segment.Initializers[index];
                var offset = firstIndexOffset + index * 2;
                await Assert.That(expression.Length).IsEqualTo(2);
                await Assert.That(expression[0].Opcode).IsEqualTo(new OpcodeKey(0, 0xD2));
                await Assert.That(expression[0].Index).IsEqualTo(index == 0 ? 0u : uint.MaxValue);
                await Assert.That(expression[0].ByteOffset).IsEqualTo(offset);
                await Assert.That(expression[1].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
                await Assert.That(expression[1].ByteOffset).IsEqualTo(offset);
            }
            if (mode == ElementMode.Active)
            {
                await Assert.That(segment.ActiveTarget!.TableIndex).IsEqualTo(tableIndex);
                await Assert.That(segment.ActiveTarget.OffsetExpression.Length).IsEqualTo(2);
                await Assert
                    .That(segment.ActiveTarget.OffsetExpression[0].Immediate.AsI32())
                    .IsEqualTo(4);
                await Assert
                    .That(segment.ActiveTarget.OffsetExpression[0].ByteOffset)
                    .IsEqualTo(tableIndex == 0 ? 42L : 47L);
            }
            else
            {
                await Assert.That(segment.ActiveTarget).IsNull();
            }
            await Assert.That(followingByte).IsEqualTo((byte)0xFF);
        }
    }

    [Test]
    [Arguments("0441040B", ElementMode.Active, WasmValueKind.FuncRef, 0u)]
    [Arguments("0570", ElementMode.Passive, WasmValueKind.FuncRef, 0u)]
    [Arguments("056F", ElementMode.Passive, WasmValueKind.ExternRef, 0u)]
    [Arguments("06810041040B70", ElementMode.Active, WasmValueKind.FuncRef, 1u)]
    [Arguments("06810041040B6F", ElementMode.Active, WasmValueKind.ExternRef, 1u)]
    [Arguments("0770", ElementMode.Declarative, WasmValueKind.FuncRef, 0u)]
    [Arguments("076F", ElementMode.Declarative, WasmValueKind.ExternRef, 0u)]
    public async Task 式形式の要素列がある_参照型と式の順序と入力位置を保持する(
        string prefix,
        ElementMode mode,
        WasmValueKind elementType,
        uint tableIndex
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("01" + prefix + "02D0700BD06F0BFF");
        var firstExpressionOffset = 42L + prefix.Length / 2;
        var reader = new ModuleBinaryReader(bytes, 40, 9);

        // Act
        var segments = ModuleDecoder.ReadElement(ref reader, 100);
        var followingByte = reader.ReadByte();
        Array.Clear(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(1);
            var segment = segments[0];
            await Assert.That(segment.Mode).IsEqualTo(mode);
            await Assert.That(segment.ElementType).IsEqualTo(elementType);
            await Assert.That(segment.ByteOffset).IsEqualTo(41L);
            await Assert.That(segment.Initializers.Length).IsEqualTo(2);
            for (var index = 0; index < segment.Initializers.Length; index++)
            {
                var expression = segment.Initializers[index];
                await Assert.That(expression.Length).IsEqualTo(2);
                await Assert.That(expression[0].Opcode).IsEqualTo(new OpcodeKey(0, 0xD0));
                await Assert
                    .That(expression[0].Immediate)
                    .IsEqualTo(
                        index == 0 ? WasmValue.FromFuncRef(null) : WasmValue.FromExternRef(null)
                    );
                await Assert
                    .That(expression[0].ByteOffset)
                    .IsEqualTo(firstExpressionOffset + index * 3);
                await Assert.That(expression[1].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
                await Assert
                    .That(expression[1].ByteOffset)
                    .IsEqualTo(firstExpressionOffset + index * 3 + 2);
            }
            if (mode == ElementMode.Active)
            {
                await Assert.That(segment.ActiveTarget!.TableIndex).IsEqualTo(tableIndex);
                await Assert
                    .That(segment.ActiveTarget.OffsetExpression[0].Immediate.AsI32())
                    .IsEqualTo(4);
                await Assert
                    .That(segment.ActiveTarget.OffsetExpression[0].ByteOffset)
                    .IsEqualTo(tableIndex == 0 ? 42L : 44L);
            }
            else
            {
                await Assert.That(segment.ActiveTarget).IsNull();
            }
            await Assert.That(followingByte).IsEqualTo((byte)0xFF);
        }
    }

    [Test]
    [Arguments("0041000B00")]
    [Arguments("010000")]
    [Arguments("020041000B0000")]
    [Arguments("030000")]
    [Arguments("0441000B00")]
    [Arguments("057000")]
    [Arguments("060041000B6F00")]
    [Arguments("076F00")]
    public async Task フラグが非最短u32で初期化式列が空である_全形式を読み取る(string segment)
    {
        // Arrange
        var bytes = Convert.FromHexString("8100" + segment[..2] + "00" + segment[2..]);
        bytes[2] |= 0x80;
        var reader = new ModuleBinaryReader(bytes, 40, 9);

        // Act
        var segments = ModuleDecoder.ReadElement(ref reader, 100);
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(1);
            await Assert.That(segments[0].ByteOffset).IsEqualTo(42L);
            await Assert.That(segments[0].Initializers.IsDefault).IsFalse();
            await Assert.That(segments[0].Initializers.IsEmpty).IsTrue();
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(ModuleReadMode.Bounded)]
    [Arguments(ModuleReadMode.Diagnostic)]
    public async Task 要素セグメント数が0である_空の定義を返す(ModuleReadMode mode)
    {
        // Arrange
        var reader = new ModuleBinaryReader([0, 0xFF], 40, 9, mode: mode);

        // Act
        var segments = ModuleDecoder.ReadElement(ref reader, 100);
        var followingByte = reader.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(0);
            await Assert.That(followingByte).IsEqualTo((byte)0xFF);
        }
    }

    [Test]
    public async Task 型と添字と定数式規則が不正である_静的検証をせず後続検証へ渡す()
    {
        // Arrange
        var bytes = Convert.FromHexString(
            "0206FFFFFFFF0F42000B6F030141000B23FFFFFFFF0F0BD2FFFFFFFF0F0B010001FFFFFFFF0F"
        );
        var reader = new ModuleBinaryReader(bytes, 40, 9);

        // Act
        var segments = ModuleDecoder.ReadElement(ref reader, 100);
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(segments.Count).IsEqualTo(2);
            await Assert.That(segments[0].ActiveTarget!.TableIndex).IsEqualTo(uint.MaxValue);
            await Assert
                .That(segments[0].ActiveTarget!.OffsetExpression[0].Immediate.Kind)
                .IsEqualTo(WasmValueKind.I64);
            await Assert.That(segments[0].ElementType).IsEqualTo(WasmValueKind.ExternRef);
            await Assert
                .That(segments[0].Initializers[0][1].Immediate.Kind)
                .IsEqualTo(WasmValueKind.I32);
            await Assert
                .That(segments[0].Initializers[0][0].Opcode)
                .IsEqualTo(new OpcodeKey(0, 0x01));
            await Assert
                .That(segments[0].Initializers[1][0].Opcode)
                .IsEqualTo(new OpcodeKey(0, 0x23));
            await Assert.That(segments[0].Initializers[1][0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(segments[0].Initializers[2][0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(segments[1].Mode).IsEqualTo(ElementMode.Passive);
            await Assert.That(segments[1].Initializers[0][0].Index).IsEqualTo(uint.MaxValue);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("0108", 41L, "malformed elements segment kind")]
    [Arguments("018800", 42L, "malformed elements segment kind")]
    [Arguments("01FFFFFFFF0F", 45L, "malformed elements segment kind")]
    [Arguments("01010100", 42L, "malformed element kind")]
    [Arguments("010241000B0100", 45L, "malformed element kind")]
    [Arguments("0103800000", 42L, "malformed element kind")]
    [Arguments("01057F00", 42L, "malformed reference type")]
    [Arguments("01060041000B6E00", 46L, "malformed reference type")]
    [Arguments("01077B00", 42L, "malformed reference type")]
    [Arguments("0105F00000", 43L, "integer representation too long")]
    [Arguments("808080808000", 45L, "integer representation too long")]
    [Arguments("018080808010", 45L, "integer too large")]
    [Arguments("0102808080801000", 46L, "integer too large")]
    [Arguments("010100018080808010", 48L, "integer too large")]
    [Arguments("0105708080808010", 47L, "integer too large")]
    [Arguments("010541000B", 42L, "malformed reference type")]
    [Arguments("01057001D07005", 46L, "END opcode expected")]
    [Arguments("03", 40L, "length out of bounds")]
    [Arguments("01010003", 43L, "length out of bounds")]
    [Arguments("01057003", 43L, "length out of bounds")]
    public async Task 構文の値か符号化が不正である_Decode診断と入力位置を保持する(
        string hex,
        long offset,
        string message
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
                    var reader = new ModuleBinaryReader(bytes, 40, 9, mode: mode);
                    ModuleDecoder.ReadElement(ref reader, 100);
                })
                .ThrowsExactly<WasmDecodeException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith(message, StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, 9));
            }
        }
    }

    [Test]
    [Arguments("")]
    [Arguments("80")]
    [Arguments("01")]
    [Arguments("0180")]
    [Arguments("0102")]
    [Arguments("010280")]
    [Arguments("01024100")]
    [Arguments("010241000B")]
    [Arguments("0101")]
    [Arguments("010100")]
    [Arguments("0101000180")]
    [Arguments("0105")]
    [Arguments("010570")]
    [Arguments("01057001D070")]
    [Arguments("01044100")]
    public async Task 宣言か式が途中で切れる_両読取モードで終端位置を保持する(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            // Act & Assert
            Action read = () =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 9, mode: mode);
                ModuleDecoder.ReadElement(ref reader, 100);
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
                    .IsEqualTo(new(WasmProcessingStage.Decode, 40L + bytes.Length, null, 9));
            }
        }
    }

    [Test]
    [Arguments("010041000B00", 4u)]
    [Arguments("01057001D0700B", 6u)]
    public async Task 宣言範囲の後に式の終端がある_通常解析で後続を読まない(string hex, uint length)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var parent = new ModuleBinaryReader(bytes, 40, 9);
                var reader = parent.ReadRange(length);
                ModuleDecoder.ReadElement(ref reader, 100);
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, 40L + length, null, 9));
    }

    [Test]
    [Arguments("0100FD0C", 44L)]
    [Arguments("01057001FD0C", 46L)]
    public async Task 要素式のV128定数が切断される_両読取モードで即値先頭と要素セクションの位置を保持する(
        string hex,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            Action read = () =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 9, mode: mode);
                ModuleDecoder.ReadElement(ref reader, 100);
            };

            // Act & Assert
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
                    .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, 9));
            }
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 要素式に完全なV128定数がある_即値をDecodeして要求型との不一致をValidateで返す(
        bool initializer
    )
    {
        // Arrange
        byte[] prefix = initializer ? [1, 5, 0x70, 1] : [1, 0];
        byte[] suffix = initializer ? [] : [0];
        byte[] payload = [.. prefix, 0xFD, 0x0C, .. new byte[16], 0x0B, .. suffix];
        foreach (var mode in new[] { ModuleReadMode.Bounded, ModuleReadMode.Diagnostic })
        {
            var reader = new ModuleBinaryReader(payload, 40, 9, mode: mode);

            // Act
            var segments = ModuleDecoder.ReadElement(ref reader, 100);
            var expression = initializer
                ? segments[0].Initializers[0]
                : segments[0].ActiveTarget!.OffsetExpression;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(expression[0].Immediate.AsV128()).IsEqualTo((0UL, 0UL));
                await Assert.That(expression[0].ByteOffset).IsEqualTo(40L + prefix.Length);
                await Assert.That(expression[^1].ByteOffset).IsEqualTo(58L + prefix.Length);
            }
        }
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Tables((0x70, 0, null)),
                HostLinkingModuleBinary.Section(9, payload)
            )
        );
        var expectedOffset = initializer
            ? module.ElementSegments[0].Initializers[0][^1].ByteOffset
            : module.ElementSegments[0].ActiveTarget!.OffsetExpression[^1].ByteOffset;

        // Act & Assert
        var failure = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(failure!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(failure.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, expectedOffset, null, 9));
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
