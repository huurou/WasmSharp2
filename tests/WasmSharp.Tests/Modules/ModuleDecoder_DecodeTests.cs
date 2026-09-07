using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleDecoder_DecodeTests
{
    [Test]
    public async Task 型にCore2の全値型がある_引数と結果を順番どおり保持する()
    {
        // Arrange
        var bytes = Convert.FromHexString(
            "0061736D0100000001120160077F7E7D7C7B706F077F7E7D7C7B706F"
        );
        WasmValueKind[] expected =
        [
            WasmValueKind.I32,
            WasmValueKind.I64,
            WasmValueKind.F32,
            WasmValueKind.F64,
            WasmValueKind.V128,
            WasmValueKind.FuncRef,
            WasmValueKind.ExternRef,
        ];

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        await Assert.That(module.Types.Length).IsEqualTo(1);
        using (Assert.Multiple())
        {
            await Assert.That(module.Types[0].Parameters.Length).IsEqualTo(7);
            await Assert.That(module.Types[0].Results.Length).IsEqualTo(7);
            for (var index = 0; index < expected.Length; index++)
            {
                await Assert.That(module.Types[0].Parameters[index]).IsEqualTo(expected[index]);
                await Assert.That(module.Types[0].Results[index]).IsEqualTo(expected[index]);
            }
        }
    }

    [Test]
    [Arguments("")]
    [Arguments("00020161")]
    public async Task Customを各所に配置しexport名と添字が未検証_構文だけ読み定義を保持する(
        string custom
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(
            "0061736D01000000"
                + custom
                + "010401600000"
                + custom
                + "070B020000FFFFFFFF0F000000"
                + custom
        );
        var firstExportOffset = 17L + 2 * (custom.Length / 2);

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types.Length).IsEqualTo(1);
            await Assert.That(module.Types[0].Parameters.Length).IsEqualTo(0);
            await Assert.That(module.Types[0].Results.Length).IsEqualTo(0);
            await Assert.That(module.Exports.Length).IsEqualTo(2);
            await Assert.That(module.Exports[0].Name).IsEqualTo("");
            await Assert.That(module.Exports[1].Name).IsEqualTo("");
            await Assert.That(module.Exports[0].FunctionIndex).IsEqualTo(uint.MaxValue);
            await Assert.That(module.Exports[1].FunctionIndex).IsEqualTo(0u);
            await Assert.That(module.Exports[0].ByteOffset).IsEqualTo(firstExportOffset);
            await Assert.That(module.InputLength).IsEqualTo((long)bytes.Length);
        }
    }

    [Test]
    public async Task Customに未知命令に見える内容がある_名前の後の任意バイトを読み飛ばす()
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D0100000000060161FF0B0D00010401600000");

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        await Assert.That(module.Types.Length).IsEqualTo(1);
    }

    [Test]
    [Arguments("010100010100", 11L, (byte)1)]
    [Arguments("070100010100", 11L, (byte)1)]
    [Arguments("070100020100", 11L, (byte)2)]
    [Arguments("01", 9L, (byte)1)]
    [Arguments("01FFFFFFFF0F", 14L, (byte)1)]
    [Arguments("0D00", 8L, (byte)13)]
    [Arguments("010200", 10L, (byte)1)]
    [Arguments("01020000", 11L, (byte)1)]
    [Arguments("010101", 10L, (byte)1)]
    [Arguments("010401610000", 11L, (byte)1)]
    [Arguments("01050160016E00", 13L, (byte)1)]
    [Arguments("0105FFFFFFFF0F", 10L, (byte)1)]
    [Arguments("0000", 10L, (byte)0)]
    [Arguments("000201FF", 11L, (byte)0)]
    [Arguments("07050101FF0000", 12L, (byte)7)]
    [Arguments("070401000400", 12L, (byte)7)]
    public async Task Sectionや型や名前の構文が不正_検証段階へ渡さず破損になる(
        string sections,
        long offset,
        byte sectionId
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
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, null, sectionId));
    }

    [Test]
    public async Task ヘッダーだけの入力_空の静的モジュールになる()
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000");

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types.Length).IsEqualTo(0);
            await Assert.That(module.Functions.Length).IsEqualTo(0);
            await Assert.That(module.Exports.Length).IsEqualTo(0);
            await Assert.That(module.InputLength).IsEqualTo(8L);
        }
    }

    [Test]
    [Arguments("", 0L)]
    [Arguments("006173", 3L)]
    [Arguments("0061736D010000", 7L)]
    [Arguments("0062736D01000000", 1L)]
    [Arguments("0061736D02000000", 4L)]
    public async Task ヘッダーが不正_相対位置付きの破損になる(string hex, long offset)
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes))
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Decode, offset));
    }
}
