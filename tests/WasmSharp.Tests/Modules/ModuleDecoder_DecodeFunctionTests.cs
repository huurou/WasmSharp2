using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleDecoder_DecodeTests
{
    [Test]
    [Arguments("FFFFFFFF0F", uint.MaxValue)]
    [Arguments("8000", 0u)]
    public async Task 型indexが未検証_非最短表現とu32全域を保持する(string typeIndex, uint expected)
    {
        // Arrange
        var payloadLength = (1 + typeIndex.Length / 2).ToString("X2");
        var bytes = Convert.FromHexString(
            "0061736D0100000003" + payloadLength + "01" + typeIndex + "0A040102000B"
        );

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        await Assert.That(module.Types.Length).IsEqualTo(0);
        await Assert.That(module.Functions[0].TypeIndex).IsEqualTo(expected);
    }

    [Test]
    [Arguments("0A0A010801FFFFFFFF0F7F0B", 1)]
    [Arguments("0A0C010A02FEFFFFFF0F7F017E0B", 2)]
    public async Task Locals合計がu32最大値_巨大配列へ展開せず圧縮宣言を保持する(
        string codeSection,
        int count
    )
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D0100000003020100" + codeSection);

        // Act
        var function = ModuleDecoder.Decode(bytes).Functions[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(function.Locals.Length).IsEqualTo(count);
            await Assert.That(function.Locals.Sum(x => (long)x.Count)).IsEqualTo(4294967295L);
            await Assert.That(function.Locals[0].Type).IsEqualTo(WasmValueKind.I32);
            await Assert.That(function.Instructions.Length).IsEqualTo(1);
            await Assert.That(function.Instructions[0].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
        }
    }

    [Test]
    public async Task 引数とlocalsと複数定数がある_最小実行形の判定をせず構文を読み終える()
    {
        // Arrange
        var bytes = Convert.FromHexString(
            "0061736D0100000001070160017F027F7E030201000A0A010801017F410142020B"
        );

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Types[0].Parameters.Length).IsEqualTo(1);
            await Assert.That(module.Types[0].Results.Length).IsEqualTo(2);
            await Assert.That(module.Functions[0].Locals[0].Count).IsEqualTo(1u);
            await Assert.That(module.Functions[0].Instructions.Length).IsEqualTo(3);
        }
    }

    [Test]
    [Arguments("030201000A0100", 14L, null)]
    [Arguments("0A040102000B", 10L, null)]
    [Arguments("030201000A040103000B", 16L, null)]
    [Arguments("030201000A03010100", 17L, 0u)]
    [Arguments("030201000A050103000B0B", 18L, 0u)]
    [Arguments("030201000A06010400050B00", 17L, 0u)]
    [Arguments("030201000A050103004180", 19L, 0u)]
    [Arguments("030201000A0C010A02FFFFFFFF0F7F017E0B", 23L, 0u)]
    [Arguments("030201000A0C010A02FFFFFFFF0F7F027E0B", 23L, 0u)]
    public async Task 件数やbodyやlocalsや終端が不正_関数位置付きの破損になる(
        string sections,
        long offset,
        uint? functionIndex
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
            .IsEqualTo(new(WasmProcessingStage.Decode, offset, functionIndex, 10));
    }

    [Test]
    public async Task Functionに対応するcodeがない_件数不一致を破損にする()
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D0100000003020100");

        // Act & Assert
        await Assert.That(() => ModuleDecoder.Decode(bytes)).ThrowsExactly<WasmDecodeException>();
    }

    [Test]
    [Arguments((byte)0x7F, "41FFFFFFFF070B", 0x7FFFFFFFUL)]
    [Arguments((byte)0x7E, "428080808080808080807F0B", 0x8000000000000000UL)]
    [Arguments((byte)0x7D, "434523C1FF0B", 0xFFC12345UL)]
    [Arguments((byte)0x7C, "44BC9A78563412F8FF0B", 0xFFF8123456789ABCUL)]
    public async Task 定数関数を読む_即値のビットと入力位置を保持する(
        byte resultType,
        string instructions,
        ulong expectedBits
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(resultType, Convert.FromHexString(instructions));

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        await Assert.That(module.Functions.Length).IsEqualTo(1);
        var function = module.Functions[0];
        var value = function.Instructions[0].Immediate;
        var bits = value.Kind switch
        {
            WasmValueKind.I32 => unchecked((uint)value.AsI32()),
            WasmValueKind.I64 => unchecked((ulong)value.AsI64()),
            WasmValueKind.F32 => value.AsF32Bits(),
            WasmValueKind.F64 => value.AsF64Bits(),
            _ => throw new InvalidOperationException(),
        };
        using (Assert.Multiple())
        {
            await Assert.That(function.TypeIndex).IsEqualTo(0u);
            await Assert.That(function.BodyOffset).IsEqualTo(32L);
            await Assert.That(function.Locals.Length).IsEqualTo(0);
            await Assert.That(function.Instructions.Length).IsEqualTo(2);
            await Assert.That(bits).IsEqualTo(expectedBits);
            await Assert.That(function.Instructions[0].ByteOffset).IsEqualTo(33L);
            await Assert.That(function.Instructions[1].Opcode).IsEqualTo(new OpcodeKey(0, 0x0B));
            await Assert
                .That(function.Instructions[1].ByteOffset)
                .IsEqualTo((long)bytes.Length - 1);
        }
    }

    [Test]
    public async Task 複数関数がある_定義順と型indexと別の定数を保持する()
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(
            [(0x7F, [0x41, 0x2A, 0x0B]), (0x7E, [0x42, 0x7F, 0x0B])],
            [("先頭", 0), ("末尾", 1)]
        );

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(module.Functions.Length).IsEqualTo(2);
            await Assert.That(module.Functions[0].TypeIndex).IsEqualTo(0u);
            await Assert.That(module.Functions[1].TypeIndex).IsEqualTo(1u);
            await Assert.That(module.Functions[0].Instructions[0].Immediate.AsI32()).IsEqualTo(42);
            await Assert.That(module.Functions[1].Instructions[0].Immediate.AsI64()).IsEqualTo(-1L);
            await Assert.That(module.Exports[0].Name).IsEqualTo("先頭");
            await Assert.That(module.Exports[1].Name).IsEqualTo("末尾");
        }
    }

    [Test]
    [Arguments("030100")]
    [Arguments("0A0100")]
    [Arguments("0301000A0100")]
    public async Task 関数0個のsection_省略と同じ空の定義になる(string sections)
    {
        // Arrange
        var bytes = Convert.FromHexString("0061736D01000000" + sections);

        // Act
        var module = ModuleDecoder.Decode(bytes);

        // Assert
        await Assert.That(module.Functions.Length).IsEqualTo(0);
    }
}
