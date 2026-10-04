using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class ModuleBinaryReader_ReadF32BitsTests
{
    [Test]
    public async Task 浮動小数点のバイト列_リトルエンディアンのビットを保持する()
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString("4523C1FF"));

        // Act
        var bits = reader.ReadF32Bits();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bits).IsEqualTo(0xFFC12345u);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 固定幅に足りない_元の診断を持つ境界通知になる()
    {
        // Arrange
        var bytes = new byte[3];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 30);
                reader.ReadF32Bits();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert.That(exception!.Fallback.Location!.ByteOffset).IsEqualTo(30L);
    }
}

internal class ModuleBinaryReader_ReadF64BitsTests
{
    [Test]
    public async Task 浮動小数点のバイト列_リトルエンディアンのビットを保持する()
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString("BC9A78563412F8FF"));

        // Act
        var bits = reader.ReadF64Bits();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bits).IsEqualTo(0xFFF8123456789ABCUL);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    public async Task 固定幅に足りない_元の診断を持つ境界通知になる()
    {
        // Arrange
        var bytes = new byte[7];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 30);
                reader.ReadF64Bits();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert.That(exception!.Fallback.Location!.ByteOffset).IsEqualTo(30L);
    }
}

internal class ModuleBinaryReader_ReadNameTests
{
    [Test]
    [Arguments("00", "")]
    [Arguments("077200E697A5C2A2", "r\0日¢")]
    [Arguments("04F09F9880", "😀")]
    [Arguments("810061", "a")]
    public async Task 正しいUTF8名_空とヌルと多バイト文字を保持する(string hex, string expected)
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex));

        // Act
        var name = reader.ReadName();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(name).IsEqualTo(expected);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("01FF")]
    [Arguments("02C0AF")]
    [Arguments("03EDA080")]
    [Arguments("04F4908080")]
    [Arguments("02E697")]
    public async Task UTF8が不正_置換せず位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 7);
                reader.ReadName();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 41, null, 7));
            await Assert
                .That(
                    exception.Message.StartsWith(
                        "malformed UTF-8 encoding",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task 名前の宣言長が残量を超える_元の診断を持つ境界通知になる()
    {
        // Arrange
        var bytes = Convert.FromHexString("0261");

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 40, 7);
                reader.ReadName();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, 41, null, 7));
    }
}

internal class ModuleBinaryReader_ReadU32Tests
{
    [Test]
    [Arguments("", 20L)]
    [Arguments("80", 21L)]
    public async Task 診断用モードの物理EOF_内部境界通知を出さずDecode失敗にする(
        string hex,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2, ModuleReadMode.Diagnostic);
                reader.ReadU32();
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
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
        }
    }

    [Test]
    [Arguments("8080808080", "integer representation too long", 25L)]
    [Arguments("FFFFFFFF8F00", "integer representation too long", 25L)]
    [Arguments("FFFFFFFF10", "integer too large", 24L)]
    [Arguments("FFFFFFFF90", "integer too large", 24L)]
    public async Task 最終payloadと継続bitが不正_値超過を先に選び追加読取りしない(
        string hex,
        string prefix,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadU32();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
        }
    }

    [Test]
    [Arguments("00", 0u)]
    [Arguments("7F", 127u)]
    [Arguments("8001", 128u)]
    [Arguments("FFFFFFFF0F", uint.MaxValue)]
    [Arguments("8080808000", 0u)]
    [Arguments("8180808000", 1u)]
    public async Task 合法なLEB表現_境界値と非最短表現を読み取る(string hex, uint expected)
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex));

        // Act
        var actual = reader.ReadU32();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsEqualTo(expected);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("FFFFFFFF10")]
    [Arguments("808080808000")]
    [Arguments("FFFFFFFF8F")]
    public async Task 幅や未使用ビットが不正_位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadU32();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.Location.SectionId).IsEqualTo((byte?)10);
            await Assert.That(exception.Location.FunctionIndex).IsEqualTo((uint?)2);
            await Assert.That(exception.Location.ByteOffset!.Value).IsGreaterThanOrEqualTo(20L);
        }
    }

    [Test]
    [Arguments("", 20L)]
    [Arguments("80", 21L)]
    public async Task 整数が途中で終わる_元の診断を持つ境界通知になる(string hex, long offset)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadU32();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
    }
}

internal class ModuleBinaryReader_ReadS32Tests
{
    [Test]
    [Arguments("8080808080", "integer representation too long", 25L)]
    [Arguments("FFFFFFFFFF00", "integer representation too long", 25L)]
    [Arguments("FFFFFFFF08", "integer too large", 24L)]
    [Arguments("80808080F7", "integer too large", 24L)]
    public async Task 最終payloadと継続bitが不正_符号拡張違反を先に選ぶ(
        string hex,
        string prefix,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadS32();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
        }
    }

    [Test]
    [Arguments("00", 0)]
    [Arguments("7F", -1)]
    [Arguments("C000", 64)]
    [Arguments("40", -64)]
    [Arguments("8080808078", int.MinValue)]
    [Arguments("FFFFFFFF07", int.MaxValue)]
    [Arguments("FFFFFFFF7F", -1)]
    [Arguments("8080808000", 0)]
    public async Task 合法なLEB表現_境界値と非最短表現を読み取る(string hex, int expected)
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex));

        // Act
        var actual = reader.ReadS32();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsEqualTo(expected);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("FFFFFFFF08")]
    [Arguments("8080808077")]
    [Arguments("808080808000")]
    public async Task 幅や未使用ビットが不正_位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadS32();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.Location.SectionId).IsEqualTo((byte?)10);
            await Assert.That(exception.Location.FunctionIndex).IsEqualTo((uint?)2);
            await Assert.That(exception.Location.ByteOffset!.Value).IsGreaterThanOrEqualTo(20L);
        }
    }

    [Test]
    [Arguments("", 20L)]
    [Arguments("80", 21L)]
    public async Task 整数が途中で終わる_元の診断を持つ境界通知になる(string hex, long offset)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadS32();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
    }
}

internal class ModuleBinaryReader_ReadS64Tests
{
    [Test]
    [Arguments("80808080808080808080", "integer representation too long", 30L)]
    [Arguments("FFFFFFFFFFFFFFFFFFFF00", "integer representation too long", 30L)]
    [Arguments("80808080808080808001", "integer too large", 29L)]
    [Arguments("FFFFFFFFFFFFFFFFFFFE", "integer too large", 29L)]
    public async Task 最終payloadと継続bitが不正_64bitの原因と位置を区別する(
        string hex,
        string prefix,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadS64();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
        }
    }

    [Test]
    [Arguments("00", 0L)]
    [Arguments("7F", -1L)]
    [Arguments("8080808080808080807F", long.MinValue)]
    [Arguments("FFFFFFFFFFFFFFFFFF00", long.MaxValue)]
    [Arguments("FFFFFFFFFFFFFFFFFF7F", -1L)]
    [Arguments("80808080808080808000", 0L)]
    public async Task 合法なLEB表現_境界値と非最短表現を読み取る(string hex, long expected)
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex));

        // Act
        var actual = reader.ReadS64();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsEqualTo(expected);
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("80808080808080808001")]
    [Arguments("FFFFFFFFFFFFFFFFFF7E")]
    [Arguments("8080808080808080808000")]
    public async Task 幅や未使用ビットが不正_位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadS64();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Decode);
            await Assert.That(exception.Location.SectionId).IsEqualTo((byte?)10);
            await Assert.That(exception.Location.FunctionIndex).IsEqualTo((uint?)2);
            await Assert.That(exception.Location.ByteOffset!.Value).IsGreaterThanOrEqualTo(20L);
        }
    }

    [Test]
    [Arguments("", 20L)]
    [Arguments("80", 21L)]
    public async Task 整数が途中で終わる_元の診断を持つ境界通知になる(string hex, long offset)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20, 10, 2);
                reader.ReadS64();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, 2, 10));
    }
}

internal class ModuleBinaryReader_TryPeekByteTests
{
    [Test]
    [Arguments("", false, (byte)0)]
    [Arguments("AB", true, (byte)0xAB)]
    public async Task 次byteを確認する_位置を進めず終端を区別する(
        string hex,
        bool expected,
        byte expectedValue
    )
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex), 20);

        // Act
        var found = reader.TryPeekByte(out var value);
        var position = reader.Position;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(found).IsEqualTo(expected);
            await Assert.That(value).IsEqualTo(expectedValue);
            await Assert.That(position).IsEqualTo(20L);
        }
    }
}

internal class ModuleBinaryReader_ReadLengthTests
{
    [Test]
    [Arguments("0261", 2u, 1)]
    [Arguments("8100", 1u, 0)]
    [Arguments("00", 0u, 0)]
    public async Task prefix前の物理残量以内_取得前に長さだけを返す(
        string hex,
        uint expected,
        int expectedRemaining
    )
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex), 100);

        // Act
        var length = reader.ReadLength();
        var remaining = reader.Remaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(length).IsEqualTo(expected);
            await Assert.That(remaining).IsEqualTo(expectedRemaining);
        }
    }

    [Test]
    public async Task 子の宣言残量を超える長さ_原入力の物理終端を上限にする()
    {
        // Arrange
        var parent = new ModuleBinaryReader([2, 0x61, 0x62], 100);
        var child = parent.ReadRange(1);

        // Act
        var length = child.ReadLength();
        var declaredRemaining = child.DeclaredRemaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(length).IsEqualTo(2u);
            await Assert.That(declaredRemaining).IsEqualTo(0L);
        }
    }

    [Test]
    public async Task prefix前の物理残量超過_長さprefixの位置を通知する()
    {
        // Arrange
        byte[] bytes = [3, 0x61];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 100, 7);
                reader.ReadLength();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith("length out of bounds", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Decode, 100, null, 7));
        }
    }
}

internal class ModuleBinaryReader_ReadU1Tests
{
    [Test]
    [Arguments("00", 0u)]
    [Arguments("01", 1u)]
    public async Task 許容payload_1bit整数を取得する(string hex, uint expected)
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex));

        // Act
        var actual = reader.ReadU1();

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("02", "integer too large", 20L)]
    [Arguments("82", "integer too large", 20L)]
    [Arguments("80", "integer representation too long", 21L)]
    [Arguments("8100", "integer representation too long", 21L)]
    public async Task 値超過と継続bit_追加読取りせず原因を区別する(
        string hex,
        string prefix,
        long offset
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20);
                reader.ReadU1();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert.That(exception.Location!.ByteOffset).IsEqualTo(offset);
        }
    }
}

internal class ModuleBinaryReader_ReadS7Tests
{
    [Test]
    [Arguments("00", 0)]
    [Arguments("3F", 63)]
    [Arguments("40", -64)]
    [Arguments("7F", -1)]
    public async Task 許容payload_7bitの符号を拡張する(string hex, int expected)
    {
        // Arrange
        var reader = new ModuleBinaryReader(Convert.FromHexString(hex));

        // Act
        var actual = reader.ReadS7();

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("80")]
    [Arguments("FF00")]
    public async Task 継続bitがある_次byte取得前に表現長超過になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 20);
                reader.ReadS7();
            })
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "integer representation too long",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert.That(exception.Location!.ByteOffset).IsEqualTo(21L);
        }
    }
}

internal class ModuleBinaryReader_ReadRangeTests
{
    [Test]
    public async Task 診断用モードの整数が宣言境界を越える_物理byteと独立した宣言終端を保持する()
    {
        // Arrange
        var parent = new ModuleBinaryReader([0x80, 0x01], 100, 3, mode: ModuleReadMode.Diagnostic);

        // Act
        var child = parent.ReadRange(1, 2);
        var value = child.ReadU32();
        var childEnd = child.DeclaredEnd;
        var childRemaining = child.DeclaredRemaining;
        var childPosition = child.Position;
        var parentPosition = parent.Position;
        var next = parent.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(value).IsEqualTo(128u);
            await Assert.That(childEnd).IsEqualTo(101L);
            await Assert.That(childRemaining).IsEqualTo(-1L);
            await Assert.That(childPosition).IsEqualTo(102L);
            await Assert.That(parentPosition).IsEqualTo(101L);
            await Assert.That(next).IsEqualTo((byte)1);
        }
    }

    [Test]
    public async Task 診断用モードの名前長が宣言境界を越える_同じ長さとUTF8処理を使う()
    {
        // Arrange
        var parent = new ModuleBinaryReader(
            [0x81, 0, 0x61],
            100,
            0,
            mode: ModuleReadMode.Diagnostic
        );
        var child = parent.ReadRange(1);

        // Act
        var name = child.ReadName();
        var remaining = child.DeclaredRemaining;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(name).IsEqualTo("a");
            await Assert.That(remaining).IsEqualTo(-2L);
        }
    }

    [Test]
    public async Task 診断用モードの宣言長が物理残量を超える_親を物理EOFに留め子の宣言終端を保持する()
    {
        // Arrange
        var parent = new ModuleBinaryReader([0x11, 0x22], 100, 10, mode: ModuleReadMode.Diagnostic);

        // Act
        var child = parent.ReadRange(uint.MaxValue, 3);
        var parentPosition = parent.Position;
        var parentRemaining = parent.Remaining;
        var declaredEnd = child.DeclaredEnd;
        var inputEnd = child.InputEnd;
        var first = child.ReadByte();
        var second = child.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(parentPosition).IsEqualTo(102L);
            await Assert.That(parentRemaining).IsEqualTo(0);
            await Assert.That(declaredEnd).IsEqualTo(4294967395L);
            await Assert.That(inputEnd).IsEqualTo(102L);
            await Assert.That(first).IsEqualTo((byte)0x11);
            await Assert.That(second).IsEqualTo((byte)0x22);
        }
    }

    [Test]
    public async Task 部分範囲を読む_親の次位置と子の元位置を保持する()
    {
        // Arrange
        var reader = new ModuleBinaryReader([0x11, 0x22, 0x33], 100, 10);

        // Act
        var child = reader.ReadRange(2, 3);
        var first = child.ReadByte();
        var second = child.ReadByte();
        var childLocation = child.Location();
        var remaining = child.Remaining;
        var inputEnd = child.InputEnd;
        var declaredEnd = child.DeclaredEnd;
        var declaredRemaining = child.DeclaredRemaining;
        var parentPosition = reader.Position;
        var last = reader.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsEqualTo((byte)0x11);
            await Assert.That(second).IsEqualTo((byte)0x22);
            await Assert.That(last).IsEqualTo((byte)0x33);
            await Assert.That(remaining).IsEqualTo(0);
            await Assert.That(inputEnd).IsEqualTo(103L);
            await Assert.That(declaredEnd).IsEqualTo(102L);
            await Assert.That(declaredRemaining).IsEqualTo(0L);
            await Assert.That(parentPosition).IsEqualTo(102L);
            await Assert.That(childLocation).IsEqualTo(new(WasmProcessingStage.Decode, 102, 3, 10));
        }
    }

    [Test]
    [Arguments(2u)]
    [Arguments(uint.MaxValue)]
    public async Task 宣言長が残量を超える_縮小変換せず元の診断を持つ境界通知になる(uint length)
    {
        // Arrange
        byte[] bytes = [0x11];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 100, 10, 3);
                reader.ReadRange(length);
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert
            .That(exception!.Fallback.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, 100, 3, 10));
    }

    [Test]
    public async Task 子の末尾を越える_親の後続バイトを読み取らない()
    {
        // Arrange
        byte[] bytes = [0x11, 0x22];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new ModuleBinaryReader(bytes, 100);
                var child = reader.ReadRange(1);
                child.ReadByte();
                child.ReadByte();
            })
            .ThrowsExactly<ModuleReadBoundaryException>();
        await Assert.That(exception!.Fallback.Location!.ByteOffset).IsEqualTo(101L);
    }
}
