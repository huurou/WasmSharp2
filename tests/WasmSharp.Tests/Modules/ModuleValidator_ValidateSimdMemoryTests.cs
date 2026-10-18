using System.Numerics;
using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments((byte)0x00, 16)]
    [Arguments((byte)0x01, 8)]
    [Arguments((byte)0x02, 8)]
    [Arguments((byte)0x03, 8)]
    [Arguments((byte)0x04, 8)]
    [Arguments((byte)0x05, 8)]
    [Arguments((byte)0x06, 8)]
    [Arguments((byte)0x07, 1)]
    [Arguments((byte)0x08, 2)]
    [Arguments((byte)0x09, 4)]
    [Arguments((byte)0x0A, 8)]
    [Arguments((byte)0x0B, 16)]
    [Arguments((byte)0x54, 1)]
    [Arguments((byte)0x55, 2)]
    [Arguments((byte)0x56, 4)]
    [Arguments((byte)0x57, 8)]
    [Arguments((byte)0x58, 1)]
    [Arguments((byte)0x59, 2)]
    [Arguments((byte)0x5A, 4)]
    [Arguments((byte)0x5B, 8)]
    [Arguments((byte)0x5C, 4)]
    [Arguments((byte)0x5D, 8)]
    public async Task SIMDメモリ接続の各命令でalignmentを検証する_アクセス幅の上限を受理し1超過を元位置で拒否する(
        byte opcode,
        int width
    )
    {
        // Arrange
        var alignment = (byte)BitOperations.Log2((uint)width);
        var valid = CreateSimdMemoryValidationModule(opcode, alignment, 0);
        var invalid = CreateSimdMemoryValidationModule(opcode, (byte)(alignment + 1), 0);

        // Act
        valid.Validate();

        // Assert
        await Assert.That(valid.FunctionCodes.IsEmpty).IsFalse();
        await AssertSimdMemoryValidationFailure(
            invalid,
            "alignment must not be larger than natural"
        );
    }

    [Test]
    [Arguments((byte)0x54, 16)]
    [Arguments((byte)0x55, 8)]
    [Arguments((byte)0x56, 4)]
    [Arguments((byte)0x57, 2)]
    [Arguments((byte)0x58, 16)]
    [Arguments((byte)0x59, 8)]
    [Arguments((byte)0x5A, 4)]
    [Arguments((byte)0x5B, 2)]
    public async Task SIMDメモリ接続のlane上限を検証する_最終laneを受理し上限を元位置で拒否する(
        byte opcode,
        int count
    )
    {
        // Arrange
        var valid = CreateSimdMemoryValidationModule(opcode, 0, (byte)(count - 1));
        var invalid = CreateSimdMemoryValidationModule(opcode, 0, (byte)count);

        // Act
        valid.Validate();

        // Assert
        await Assert.That(valid.FunctionCodes.IsEmpty).IsFalse();
        await AssertSimdMemoryValidationFailure(invalid, "invalid lane index");
    }

    [Test]
    [Arguments((byte)0x00, false, true, (byte)0, (byte)0, "unknown memory 0")]
    [Arguments((byte)0x54, false, true, (byte)1, (byte)16, "unknown memory 0")]
    [Arguments((byte)0x58, false, false, (byte)0, (byte)0, "unknown memory 0")]
    [Arguments((byte)0x5C, false, true, (byte)0, (byte)0, "unknown memory 0")]
    [Arguments(
        (byte)0x54,
        true,
        true,
        (byte)1,
        (byte)16,
        "alignment must not be larger than natural"
    )]
    [Arguments((byte)0x54, true, true, (byte)0, (byte)16, "invalid lane index")]
    [Arguments(
        (byte)0x5D,
        true,
        true,
        (byte)4,
        (byte)0,
        "alignment must not be larger than natural"
    )]
    public async Task SIMDメモリ接続の静的制約が複合不正である_到達不能でもmemoryとalignmentとlaneの順に検査する(
        byte opcode,
        bool memory,
        bool unreachable,
        byte alignment,
        byte lane,
        string diagnostic
    )
    {
        // Arrange
        var module = CreateSimdMemoryValidationModule(
            opcode,
            alignment,
            lane,
            memory,
            unreachable,
            omitOperands: true
        );

        // Act & Assert
        await AssertSimdMemoryValidationFailure(module, diagnostic);
    }

    [Test]
    [Arguments((byte)0x0B, false)]
    [Arguments((byte)0x54, false)]
    [Arguments((byte)0x57, false)]
    [Arguments((byte)0x58, false)]
    [Arguments((byte)0x5B, false)]
    [Arguments((byte)0x00, true)]
    [Arguments((byte)0x54, true)]
    [Arguments((byte)0x58, true)]
    public async Task SIMDメモリ接続のoperandが逆順か不足する_Validateで型不一致を返して成功状態を確定しない(
        byte opcode,
        bool omit
    )
    {
        // Arrange
        var module = CreateSimdMemoryValidationModule(
            opcode,
            0,
            0,
            reverse: true,
            omitOperands: omit
        );

        // Act & Assert
        await AssertSimdMemoryValidationFailure(module, "type mismatch");
    }

    private static WasmModule CreateSimdMemoryValidationModule(
        byte opcode,
        byte alignment,
        byte lane,
        bool memory = true,
        bool unreachable = false,
        bool reverse = false,
        bool omitOperands = false
    )
    {
        var hasLane = opcode is >= 0x54 and <= 0x5B;
        var store = opcode == 0x0B || opcode is >= 0x58 and <= 0x5B;
        var vectorInput = store || hasLane;
        byte[] address = [0x41, 0];
        byte[] vector = [0xFD, 0x0C, .. new byte[16]];
        byte[] operands =
            omitOperands ? []
            : vectorInput ? (reverse ? [.. vector, .. address] : [.. address, .. vector])
            : address;
        byte[] laneBytes = hasLane ? [lane] : [];
        byte[] result = store ? [] : [0x1A];
        return WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                memory ? HostLinkingModuleBinary.Memories((1, null)) : [],
                HostLinkingModuleBinary.Code(
                    (
                        [],
                        [
                            .. (unreachable ? new byte[] { 0 } : []),
                            .. operands,
                            0xFD,
                            opcode,
                            alignment,
                            0,
                            .. laneBytes,
                            .. result,
                            0x0B,
                        ]
                    )
                )
            )
        );
    }

    private static async Task AssertSimdMemoryValidationFailure(
        WasmModule module,
        string diagnostic
    )
    {
        var instruction = module.Functions[0].Instructions.Last(x => x.Opcode.Prefix == 0xFD);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            // Act & Assert
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal))
                    .IsTrue();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(new(WasmProcessingStage.Validate, instruction.ByteOffset, 0, 10));
                await Assert.That(module.FunctionCodes).IsEmpty();
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}
