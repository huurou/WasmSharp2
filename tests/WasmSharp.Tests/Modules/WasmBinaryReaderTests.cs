using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal class WasmBinaryReader_ReadF32BitsTests
{
    [Test]
    public async Task 浮動小数点のバイト列_リトルエンディアンのビットを保持する()
    {
        // Arrange
        var reader = new WasmBinaryReader(Convert.FromHexString("4523C1FF"));

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
    public async Task 固定幅に足りない_位置付き破損になる()
    {
        // Arrange
        var bytes = new byte[3];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 30);
                reader.ReadF32Bits();
            })
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location!.ByteOffset).IsEqualTo(30L);
    }
}

internal class WasmBinaryReader_ReadF64BitsTests
{
    [Test]
    public async Task 浮動小数点のバイト列_リトルエンディアンのビットを保持する()
    {
        // Arrange
        var reader = new WasmBinaryReader(Convert.FromHexString("BC9A78563412F8FF"));

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
    public async Task 固定幅に足りない_位置付き破損になる()
    {
        // Arrange
        var bytes = new byte[7];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 30);
                reader.ReadF64Bits();
            })
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location!.ByteOffset).IsEqualTo(30L);
    }
}

internal class WasmBinaryReader_ReadNameTests
{
    [Test]
    [Arguments("00", "")]
    [Arguments("077200E697A5C2A2", "r\0日¢")]
    [Arguments("04F09F9880", "😀")]
    [Arguments("810061", "a")]
    public async Task 正しいUTF8名_空とヌルと多バイト文字を保持する(string hex, string expected)
    {
        // Arrange
        var reader = new WasmBinaryReader(Convert.FromHexString(hex));

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
    [Arguments("0361")]
    public async Task UTF8か宣言長が不正_置換せず位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 40, 7);
                reader.ReadName();
            })
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Decode, 41, null, 7));
    }
}

internal class WasmBinaryReader_ReadU32Tests
{
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
        var reader = new WasmBinaryReader(Convert.FromHexString(hex));

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
    [Arguments("")]
    [Arguments("80")]
    [Arguments("FFFFFFFF10")]
    [Arguments("808080808000")]
    [Arguments("FFFFFFFF8F")]
    public async Task 途中終了か幅や未使用ビットが不正_位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 20, 10, 2);
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
}

internal class WasmBinaryReader_ReadS32Tests
{
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
        var reader = new WasmBinaryReader(Convert.FromHexString(hex));

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
    [Arguments("")]
    [Arguments("80")]
    [Arguments("FFFFFFFF08")]
    [Arguments("8080808077")]
    [Arguments("808080808000")]
    public async Task 途中終了か幅や未使用ビットが不正_位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 20, 10, 2);
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
}

internal class WasmBinaryReader_ReadS64Tests
{
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
        var reader = new WasmBinaryReader(Convert.FromHexString(hex));

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
    [Arguments("")]
    [Arguments("80")]
    [Arguments("80808080808080808001")]
    [Arguments("FFFFFFFFFFFFFFFFFF7E")]
    [Arguments("8080808080808080808000")]
    public async Task 途中終了か幅や未使用ビットが不正_位置付き破損になる(string hex)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 20, 10, 2);
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
}

internal class WasmBinaryReader_ReadRangeTests
{
    [Test]
    public async Task 部分範囲を読む_親の次位置と子の元位置を保持する()
    {
        // Arrange
        var reader = new WasmBinaryReader([0x11, 0x22, 0x33], 100, 10);

        // Act
        var child = reader.ReadRange(2, 3);
        var first = child.ReadByte();
        var second = child.ReadByte();
        var childLocation = child.Location();
        var remaining = child.Remaining;
        var parentPosition = reader.Position;
        var last = reader.ReadByte();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsEqualTo((byte)0x11);
            await Assert.That(second).IsEqualTo((byte)0x22);
            await Assert.That(last).IsEqualTo((byte)0x33);
            await Assert.That(remaining).IsEqualTo(0);
            await Assert.That(parentPosition).IsEqualTo(102L);
            await Assert.That(childLocation).IsEqualTo(new(WasmProcessingStage.Decode, 102, 3, 10));
        }
    }

    [Test]
    [Arguments(2u)]
    [Arguments(uint.MaxValue)]
    public async Task 宣言長が残量を超える_加算で巡回せず位置付き破損になる(uint length)
    {
        // Arrange
        byte[] bytes = [0x11];

        // Act & Assert
        var exception = await Assert
            .That(() =>
            {
                var reader = new WasmBinaryReader(bytes, 100, 10, 3);
                reader.ReadRange(length);
            })
            .ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(exception!.Location)
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
                var reader = new WasmBinaryReader(bytes, 100);
                var child = reader.ReadRange(1);
                child.ReadByte();
                child.ReadByte();
            })
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location!.ByteOffset).IsEqualTo(101L);
    }
}
