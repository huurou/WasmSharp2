using WasmSharp.Exceptions;

namespace WasmSharp.Tests.Fixtures;

internal class HostLinkingModuleBinary_CreateTests
{
    [Test]
    public async Task 定数関数を生成する_バイト列と非seekのshortReadで公開操作を実行できる()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x41, 0x2A, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];
        var results = modules
            .Select(x => x.Validate().Instantiate([]).GetFunction("run").Invoke([]))
            .ToArray();

        // Assert
        using (Assert.Multiple())
        {
            foreach (var result in results)
            {
                await Assert.That(result.Values.Length).IsEqualTo(1);
                await Assert.That(result.Values[0].AsI32()).IsEqualTo(42);
            }
        }
    }

    [Test]
    [Arguments((byte)7, "0101FF0000", 5u, "07050101FF0000")]
    [Arguments((byte)1, "016000017F", 6u, "0106016000017F")]
    public async Task 不正なUTF8または宣言長を指定する_入力を補正せず公開Decodeで拒否する(
        byte id,
        string payload,
        uint declaredLength,
        string expectedSection
    )
    {
        // Arrange
        var section = HostLinkingModuleBinary.Section(
            id,
            Convert.FromHexString(payload),
            declaredLength
        );

        // Act
        var bytes = HostLinkingModuleBinary.Create(section);

        // Assert
        await Assert
            .That(Convert.ToHexString(bytes))
            .IsEqualTo("0061736D01000000" + expectedSection);

        // Act & Assert
        await Assert.That(() => WasmModule.Decode(bytes)).ThrowsExactly<WasmDecodeException>();
    }

    [Test]
    public async Task 未終端の本体を指定する_終端を補わず途中破損を再現する()
    {
        // Arrange
        const string EXPECTED = "0061736D010000000105016000017F030201000A05010300412A";

        // Act
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(([], [0x41, 0x2A]))
        );

        // Assert
        await Assert.That(Convert.ToHexString(bytes)).IsEqualTo(EXPECTED);

        // Act & Assert
        await Assert.That(() => WasmModule.Decode(bytes)).ThrowsExactly<WasmDecodeException>();
        await Assert
            .That(() => WasmModule.Decode(bytes[..^1]))
            .ThrowsExactly<WasmDecodeException>();
    }

    [Test]
    public async Task 不正な添字とlimitsと圧縮localsを指定する_最大uintと大小関係を補正しない()
    {
        // Arrange
        const string EXPECTED =
            "0061736D01000000"
            + "030601FFFFFFFF0F"
            + "0409017001FFFFFFFF0F00"
            + "05050101800100"
            + "070B0103E580A403FFFFFFFF0F"
            + "0805FFFFFFFF0F"
            + "0A0B010901FFFFFFFF0F7E1080";

        // Act
        var actual = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Functions(uint.MaxValue),
            HostLinkingModuleBinary.Tables((0x70, uint.MaxValue, 0)),
            HostLinkingModuleBinary.Memories((128, 0)),
            HostLinkingModuleBinary.Exports(("値", 3, uint.MaxValue)),
            HostLinkingModuleBinary.Start(uint.MaxValue),
            HostLinkingModuleBinary.Code(([(uint.MaxValue, 0x7E)], [0x10, 0x80]))
        );

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo(EXPECTED);
    }

    [Test]
    public async Task 定数関数のsectionを組み合わせる_既知のバイナリと一致する()
    {
        // Arrange
        const string EXPECTED =
            "0061736D010000000105016000017F030201000707010372756E00000A06010400412A0B";

        // Act
        var actual = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Exports(("run", 0, 0)),
            HostLinkingModuleBinary.Code(([], [0x41, 0x2A, 0x0B]))
        );

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo(EXPECTED);
    }

    [Test]
    public async Task ホスト連携の定義を組み合わせる_各sectionの型と添字と命令を順序どおり符号化する()
    {
        // Arrange
        const string EXPECTED =
            "0061736D01000000"
            + "010B0260000060027F7E027D7C"
            + "021E04016D01660000016D01740170010102016D016D020001016D0167037F01"
            + "03020101"
            + "0404016F0001"
            + "050401010203"
            + "0606017F01412A0B"
            + "0711040166000101740100016D020001670300"
            + "080100"
            + "0A0D010B02027F016F200110001A0B";

        // Act
        var actual = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([0x7F, 0x7E], [0x7D, 0x7C])),
            HostLinkingModuleBinary.Imports(
                ("m", "f", 0, HostLinkingModuleBinary.Unsigned(0)),
                ("m", "t", 1, [0x70, .. HostLinkingModuleBinary.Limits(1, 2)]),
                ("m", "m", 2, HostLinkingModuleBinary.Limits(1)),
                ("m", "g", 3, [0x7F, 0x01])
            ),
            HostLinkingModuleBinary.Functions(1),
            HostLinkingModuleBinary.Tables((0x6F, 1, null)),
            HostLinkingModuleBinary.Memories((2, 3)),
            HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0x2A, 0x0B])),
            HostLinkingModuleBinary.Exports(("f", 0, 1), ("t", 1, 0), ("m", 2, 0), ("g", 3, 0)),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Code(
                ([(2, 0x7F), (1, 0x6F)], [0x20, 0x01, 0x10, 0, 0x1A, 0x0B])
            )
        );

        // Assert
        await Assert.That(Convert.ToHexString(actual)).IsEqualTo(EXPECTED);
    }
}
