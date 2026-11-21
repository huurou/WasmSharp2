using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleDecoder_DecodeTests
{
    [Test]
    public async Task 命令数が保持上限を超える_通常解析は上限を報告し診断用の再走査は元の境界違反を保つ()
    {
        // Arrange
        byte[] instructions = [0x01, 0x01, 0x01, 0x0B];
        var types = HostLinkingModuleBinary.Types(([], []));
        var functions = HostLinkingModuleBinary.Functions(0);
        var complete = HostLinkingModuleBinary.Create(types, functions, HostLinkingModuleBinary.Code(([], instructions)));
        // 関数本体の長さをlocal宣言の1バイトにすると、先頭のnopが宣言範囲の直後に来る。
        var shortBody = HostLinkingModuleBinary.Create(
            types,
            functions,
            HostLinkingModuleBinary.Section(10, [1, 1, 0, .. instructions])
        );
        var firstInstruction = complete.LongLength - instructions.Length;

        // Act & Assert
        var limit = await Assert
            .That(() => ModuleDecoder.Decode(complete, WasmSpecVersion.Core2, maxCollectionLength: 2))
            .ThrowsExactly<WasmImplementationLimitException>();
        var boundary = await Assert
            .That(() => ModuleDecoder.Decode(shortBody, WasmSpecVersion.Core2, maxCollectionLength: 2))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(limit!.Reason).IsEqualTo(WasmImplementationLimitReason.CollectionSize);
            await Assert.That(limit.Limit).IsEqualTo(2);
            await Assert
                .That(limit.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, firstInstruction + 2, 0, 10));
            await Assert.That(boundary!.Message).StartsWith("unexpected end of section or function");
            await Assert
                .That(boundary.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, firstInstruction, 0, 10));
        }
    }

    [Test]
    [Arguments("090100", (byte)13)]
    public async Task 空要素sectionの後に未知sectionがある_未知sectionの位置で拒否する(string section, byte id)
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + section + "0D00");

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Decode, 11, null, id));
        }
    }

    [Test]
    [Arguments("FD0105")]
    [Arguments("FD00")]
    public async Task メモリ命令の即値が切断される_未対応にせずDecodeの境界位置を返す(string instructions)
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("unexpected end of section or function", StringComparison.Ordinal))
                .IsTrue();
            await Assert.That(exception.Location).IsEqualTo(new(WasmProcessingStage.Decode, bytes.Length, 0, 10));
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
    [Arguments("1080", 35L)]
    [Arguments("2380", 35L)]
    [Arguments("FCFFFFFFFF10", 38L)]
    [Arguments("FD808080808000", 39L)]
    [Arguments("056A", 33L)]
    public async Task 未割当opcodeか不正LEBか平坦else_未対応に置き換えず破損になる(string instructions, long offset)
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(0x7F, Convert.FromHexString(instructions));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Decode, offset, 0, 10));
    }

    [Test]
    [Arguments((byte)1)]
    [Arguments((byte)2)]
    [Arguments((byte)3)]
    public async Task 関数以外の既知exportの添字が欠落_破損として拒否する(byte kind)
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D0100000007030100" + kind.ToString("X2"));

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Decode, 13, null, 7));
    }

    [Test]
    [Arguments("0202FF", 11L, (byte)2)]
    [Arguments("070100020100", 11L, (byte)2)]
    [Arguments("0A01000C0100", 11L, (byte)12)]
    [Arguments("030201000B0100", 15L, (byte)11)]
    [Arguments("03020100000201610B0100", 19L, (byte)11)]
    public async Task セクションの長さや順序や件数が不正である_元位置を保持して破損を報告する(
        string sections,
        long offset,
        byte id
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleDecoder.Decode(bytes, WasmSpecVersion.Core2))
            .ThrowsExactly<WasmDecodeException>();
        await Assert.That(exception!.Location).IsEqualTo(new(WasmProcessingStage.Decode, offset, null, id));
    }
}
