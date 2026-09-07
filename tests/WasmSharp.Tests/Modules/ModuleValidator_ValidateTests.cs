using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments("60017F017F", "0041010B", "function.parameters")]
    [Arguments("6000017F", "01017F41010B", "function.locals")]
    [Arguments("6000017F", "01FFFFFFFF0F7F41010B", "function.locals")]
    [Arguments("600000", "000B", "function.results")]
    [Arguments("6000027F7E", "00410142020B", "function.results")]
    public async Task 型は一致するが実行形が範囲外_機能名と未完了検証範囲を持つ未実装になる(
        string signature,
        string body,
        string feature
    )
    {
        // Arrange
        var module = DecodeFunction(signature, body);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Feature).IsEqualTo(feature);
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(WasmProcessingStage.Validate, module.Functions[0].BodyOffset, 0, 10)
                );
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(1);
            await Assert
                .That(exception.UnverifiedRanges[0].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert
                .That(exception.UnverifiedRanges[0].StartOffset)
                .IsEqualTo(module.Functions[0].BodyOffset);
            await Assert
                .That(exception.UnverifiedRanges[0].EndOffset)
                .IsEqualTo(module.InputLength);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task 個数0のlocals宣言がある_実際のlocalsがない定数関数として受理する()
    {
        // Arrange
        var module = DecodeFunction("6000017F", "01007F41010B");

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes.Length).IsEqualTo(1);
        await Assert.That(codes[0].MaxOperandStack).IsEqualTo(1);
    }

    [Test]
    public async Task 定義関数がない_空の実行コードになる()
    {
        // Arrange
        var module = WasmModule.Decode(Convert.FromHexString("0061736D01000000"));

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes.IsDefault).IsFalse();
        await Assert.That(codes.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments("0303020001", "", 1u, (byte)10)]
    [Arguments("0303020000", "0707010372756E0002", 2u, (byte)7)]
    [Arguments("0303020000", "070D020372756E00000372756E0000", 0u, (byte)7)]
    public async Task 先頭関数が範囲外で全体の参照が不正_本体の未対応判定より先に検証失敗になる(
        string functions,
        string exports,
        uint index,
        byte section
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            Convert.FromHexString(
                "0061736D0100000001060160017F017F"
                    + functions
                    + exports
                    + "0A0B02040041010B040041020B"
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Location!.Stage).IsEqualTo(WasmProcessingStage.Validate);
        await Assert.That(exception.Location.FunctionIndex).IsEqualTo(index);
        await Assert.That(exception.Location.SectionId).IsEqualTo(section);
    }

    [Test]
    [Arguments("6000017E", "0041010B")]
    [Arguments("6000017F", "000B")]
    [Arguments("6000017F", "00410141020B")]
    [Arguments("600000", "0041010B")]
    [Arguments("6000027E7F", "00410142020B")]
    [Arguments("60017F017E", "0041010B")]
    [Arguments("6000017E", "01017F41010B")]
    public async Task 結果の型や個数や順序が不一致_実行形の未対応より先に終端位置付き検証失敗になる(
        string signature,
        string body
    )
    {
        // Arrange
        var module = DecodeFunction(signature, body);

        // Act & Assert
        var exception = await Assert
            .That(() => ModuleValidator.Validate(module))
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(new(WasmProcessingStage.Validate, module.InputLength - 1, 0, 10));
        await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments((byte)0x7F, "41FFFFFFFF070B", ExecutionOpcode.Op41, 0x7FFFFFFFUL)]
    [Arguments((byte)0x7E, "428080808080808080807F0B", ExecutionOpcode.Op42, 0x8000000000000000UL)]
    [Arguments((byte)0x7D, "434523C1FF0B", ExecutionOpcode.Op43, 0xFFC12345UL)]
    [Arguments((byte)0x7C, "44BC9A78563412F8FF0B", ExecutionOpcode.Op44, 0xFFF8123456789ABCUL)]
    public async Task 定数関数が有効_線形命令と最大operand数と元位置とビット列を保持する(
        byte resultType,
        string instructions,
        ExecutionOpcode opcode,
        ulong bits
    )
    {
        // Arrange
        var bytes = ConstantModuleBinary.Create(resultType, Convert.FromHexString(instructions));
        var module = WasmModule.Decode(bytes);

        // Act
        var codes = ModuleValidator.Validate(module);

        // Assert
        await Assert.That(codes.Length).IsEqualTo(1);
        var code = codes[0];
        var value = code.Instructions[0].Immediate;
        var actualBits = value.Kind switch
        {
            WasmValueKind.I32 => unchecked((uint)value.AsI32()),
            WasmValueKind.I64 => unchecked((ulong)value.AsI64()),
            WasmValueKind.F32 => value.AsF32Bits(),
            WasmValueKind.F64 => value.AsF64Bits(),
            _ => throw new InvalidOperationException(),
        };
        using (Assert.Multiple())
        {
            await Assert.That(code.Instructions.Length).IsEqualTo(2);
            await Assert.That(code.MaxOperandStack).IsEqualTo(1);
            await Assert.That(code.Instructions[0].Opcode).IsEqualTo(opcode);
            await Assert.That(code.Instructions[0].ByteOffset).IsEqualTo(33L);
            await Assert.That(actualBits).IsEqualTo(bits);
            await Assert.That(code.Instructions[1].Opcode).IsEqualTo(ExecutionOpcode.Op0B);
            await Assert.That(code.Instructions[1].ByteOffset).IsEqualTo(bytes.Length - 1L);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    private static WasmModule DecodeFunction(string signature, string body)
    {
        var type = Convert.FromHexString(signature);
        var code = Convert.FromHexString(body);
        return WasmModule.Decode([
            0x00,
            0x61,
            0x73,
            0x6D,
            0x01,
            0x00,
            0x00,
            0x00,
            0x01,
            (byte)(type.Length + 1),
            0x01,
            .. type,
            0x03,
            0x02,
            0x01,
            0x00,
            0x0A,
            (byte)(code.Length + 2),
            0x01,
            (byte)code.Length,
            .. code,
        ]);
    }
}
