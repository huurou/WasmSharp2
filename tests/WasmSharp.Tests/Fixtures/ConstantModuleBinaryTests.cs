namespace WasmSharp.Tests.Fixtures;

public class ConstantModuleBinary_CreateTests
{
    [Test]
    [Arguments(
        (byte)0x7F,
        new byte[] { 0x41, 0x2A, 0x0B },
        "0061736D010000000105016000017F030201000707010372756E00000A06010400412A0B"
    )]
    [Arguments(
        (byte)0x7E,
        new byte[] { 0x42, 0x7F, 0x0B },
        "0061736D010000000105016000017E030201000707010372756E00000A06010400427F0B"
    )]
    [Arguments(
        (byte)0x7D,
        new byte[] { 0x43, 0x01, 0x00, 0xC0, 0xFF, 0x0B },
        "0061736D010000000105016000017D030201000707010372756E00000A09010700430100C0FF0B"
    )]
    [Arguments(
        (byte)0x7C,
        new byte[] { 0x44, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x0B },
        "0061736D010000000105016000017C030201000707010372756E00000A0D010B004400000000000000800B"
    )]
    public async Task 定数命令を指定する_既知の最小モジュールと一致する(
        byte resultType,
        byte[] instructions,
        string expectedHex
    )
    {
        // Arrange
        var expected = Convert.FromHexString(expectedHex);

        // Act
        var actual = ConstantModuleBinary.Create(resultType, instructions);

        // Assert
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    public async Task 複数関数と同じ関数への複数exportを指定する_添字と順序を保持する()
    {
        // Arrange
        byte[] expected =
        [
            0x00,
            0x61,
            0x73,
            0x6D,
            0x01,
            0x00,
            0x00,
            0x00,
            0x01,
            0x09,
            0x02,
            0x60,
            0x00,
            0x01,
            0x7F,
            0x60,
            0x00,
            0x01,
            0x7E,
            0x03,
            0x03,
            0x02,
            0x00,
            0x01,
            0x07,
            0x0D,
            0x03,
            0x01,
            0x62,
            0x00,
            0x01,
            0x01,
            0x61,
            0x00,
            0x00,
            0x01,
            0x63,
            0x00,
            0x00,
            0x0A,
            0x0B,
            0x02,
            0x04,
            0x00,
            0x41,
            0x2A,
            0x0B,
            0x04,
            0x00,
            0x42,
            0x7F,
            0x0B,
        ];

        // Act
        var actual = ConstantModuleBinary.Create(
            [(0x7F, [0x41, 0x2A, 0x0B]), (0x7E, [0x42, 0x7F, 0x0B])],
            [("b", 1), ("a", 0), ("c", 0)]
        );

        // Assert
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    public async Task 日本語のexport名を指定する_UTF8のバイト数を長さに使う()
    {
        // Arrange
        byte[] expected = [0x07, 0x07, 0x01, 0x03, 0xE5, 0x80, 0xA4, 0x00, 0x00];

        // Act
        var actual = ConstantModuleBinary.Create([(0x7F, [0x41, 0x00, 0x0B])], [("値", 0)]);

        // Assert
        await Assert.That(actual[19..28]).IsEquivalentTo(expected);
    }

    [Test]
    public async Task 長い名前と範囲外の添字を指定する_複数バイトのLEBを保持する()
    {
        // Arrange
        var name = new string('a', 128);

        // Act
        var actual = ConstantModuleBinary.Create(
            [(0x7F, [0x41, 0x00, 0x0B])],
            [(name, uint.MaxValue)]
        );

        // Assert
        using (Assert.Multiple())
        {
            // exportのpayload長137、名前長128、関数添字u32最大値。
            await Assert
                .That(actual[19..25])
                .IsEquivalentTo([
                    (byte)0x07,
                    (byte)0x89,
                    (byte)0x01,
                    (byte)0x01,
                    (byte)0x80,
                    (byte)0x01,
                ]);
            await Assert
                .That(actual[153..159])
                .IsEquivalentTo([
                    (byte)0x00,
                    (byte)0xFF,
                    (byte)0xFF,
                    (byte)0xFF,
                    (byte)0xFF,
                    (byte)0x0F,
                ]);
        }
    }

    [Test]
    public async Task 長さと添字と命令を変更する_独立した負例を作れる()
    {
        // Arrange
        var valid = ConstantModuleBinary.Create(0x7F, [0x41, 0x2A, 0x0B]);
        var invalidLength = ConstantModuleBinary.Create(0x7F, [0x41, 0x2A, 0x0B]);
        var invalidIndex = ConstantModuleBinary.Create(0x7F, [0x41, 0x2A, 0x0B]);
        var invalidInstruction = ConstantModuleBinary.Create(0x7F, [0xFF, 0x0B]);

        // Act
        invalidLength[29] = 0x07; // code sectionの実際のpayload長は6。
        invalidIndex[18] = 0x01; // 型は1個なので型添字1は存在しない。

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(invalidLength)
                .IsEquivalentTo(
                    Convert.FromHexString(
                        "0061736D010000000105016000017F030201000707010372756E00000A07010400412A0B"
                    )
                );
            await Assert
                .That(invalidIndex)
                .IsEquivalentTo(
                    Convert.FromHexString(
                        "0061736D010000000105016000017F030201010707010372756E00000A06010400412A0B"
                    )
                );
            await Assert
                .That(invalidInstruction)
                .IsEquivalentTo(
                    Convert.FromHexString(
                        "0061736D010000000105016000017F030201000707010372756E00000A05010300FF0B"
                    )
                );
            await Assert
                .That(valid)
                .IsEquivalentTo(
                    Convert.FromHexString(
                        "0061736D010000000105016000017F030201000707010372756E00000A06010400412A0B"
                    )
                );
        }
    }
}
