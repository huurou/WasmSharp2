using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules;

namespace WasmSharp.Tests.Modules;

internal partial class FunctionValidator_ValidateTests
{
    [Test]
    [Arguments(0x7F, false)]
    [Arguments(0x7E, false)]
    [Arguments(0x7D, false)]
    [Arguments(0x7C, false)]
    [Arguments(0x7F, true)]
    [Arguments(0x7E, true)]
    [Arguments(0x7D, true)]
    [Arguments(0x7C, true)]
    public async Task selectの候補が同じスカラー型で条件がi32_型注釈の有無によらず検証して元の命令位置を保持する(
        int valueType,
        bool typed
    )
    {
        // Arrange
        var kind = (byte)valueType;
        byte[] select = typed ? [0x1C, 1, kind] : [0x1B];
        var module = DecodeControl(
            [kind, kind, 0x7F],
            [kind],
            [0x20, 0, 0x20, 1, 0x20, 2, .. select, 0x0B]
        );
        var definition = module.Functions[0];

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(code.Instructions.Length).IsEqualTo(5);
            await Assert
                .That(code.Instructions[3].Opcode)
                .IsEqualTo(typed ? ExecutionOpcode.Op1C : ExecutionOpcode.Op1B);
            await Assert
                .That(code.Instructions[3].ByteOffset)
                .IsEqualTo(definition.Instructions[3].ByteOffset);
            await Assert.That(code.MaxOperandStack).IsEqualTo(3);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.Functions[0]).IsSameReferenceAs(definition);
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("", "7B", "001B0B")]
    [Arguments("", "70", "001B0B")]
    [Arguments("", "7E", "00420141001B0B")]
    [Arguments("", "7F", "001C017F0B")]
    [Arguments("", "7E", "001B420141001B0B")]
    public async Task 到達不能のselect候補がUnknown_両候補はUnknownを保ち具体候補または型注釈には適合する(
        string parameters,
        string results,
        string body
    )
    {
        // Arrange
        var module = DecodeControl(
            Convert.FromHexString(parameters),
            Convert.FromHexString(results),
            Convert.FromHexString(body)
        );

        // Act
        var code = FunctionValidator.Validate(module, 0, 0, [module.Types[0]], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(code.Instructions.Length)
                .IsEqualTo(module.Functions[0].Instructions.Length);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
        }
    }

    [Test]
    [Arguments("7E7F7F", "2000200120021B0B", 3)]
    [Arguments("7F7F7E", "2000200120021B0B", 3)]
    [Arguments("70707F", "2000200120021B0B", 3)]
    [Arguments("6F6F7F", "2000200120021B0B", 3)]
    [Arguments("", "1B0B", 0)]
    [Arguments("", "410141001B0B", 2)]
    [Arguments("7E7F7F", "2000200120021C017F0B", 3)]
    [Arguments("7F7F7E", "2000200120021C017F0B", 3)]
    [Arguments("", "1C017F0B", 0)]
    [Arguments("7F7B7F", "2000200120021B0B", 3)]
    [Arguments("7B7B7E", "2000200120021B0B", 3)]
    [Arguments("7B7F7F", "2000200120021C017B0B", 3)]
    [Arguments("70707E", "2000200120021C01700B", 3)]
    [Arguments("6F6F7F", "2000200120021C01700B", 3)]
    [Arguments("", "1C016F0B", 0)]
    [Arguments("", "00420142011B0B", 3)]
    [Arguments("70", "00200041001B0B", 3)]
    [Arguments("", "00420041001C017F0B", 3)]
    [Arguments("", "001B420041001B450B", 5)]
    [Arguments("", "41014101410102401B0B1A1A1A0B", 4)]
    public async Task selectの型や個数が不正_到達不能や後続型でもUnsupportedへ読み替えず元の位置で拒否する(
        string parameters,
        string body,
        int failureIndex
    )
    {
        // Arrange
        var module = DecodeControl(
            Convert.FromHexString(parameters),
            [],
            Convert.FromHexString(body)
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[failureIndex].ByteOffset,
                        0,
                        10
                    )
                );
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("1C000B", 0)]
    [Arguments("1C027B700B", 2)]
    public async Task typed_selectの型ベクトル長が1以外_入力不足や後続型より先にValidateのarity診断で拒否する(
        string body,
        int typeCount
    )
    {
        // Arrange
        var module = DecodeControl([], [], Convert.FromHexString(body));
        var select = module.Functions[0].Instructions[0];

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(select.SelectTypes.Length).IsEqualTo(typeCount);
            await Assert
                .That(
                    exception!.Message.StartsWith("invalid result arity", StringComparison.Ordinal)
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, select.ByteOffset, 0, 10));
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
