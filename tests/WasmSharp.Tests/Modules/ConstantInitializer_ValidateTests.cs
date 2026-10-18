using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ConstantInitializer_ValidateTests
{
    private static readonly ReferenceValidationContext references_ =
        ReferenceValidationContext.Create(WasmModule.Decode(HostLinkingModuleBinary.Create()), 0);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task I32の定数または不変globalを指定する_データセクションの定数式として受理する(
        bool useGlobal
    )
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            useGlobal
                ? DecodedInstruction.WithIndex(new(0, 0x23), 1, 40)
                : DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(-1), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];
        WasmGlobalType[] importedGlobals =
        [
            new(WasmValueKind.I64, false),
            new(WasmValueKind.I32, false),
        ];

        // Act & Assert
        await Assert
            .That(() =>
                ConstantInitializer.Validate(
                    expression,
                    WasmValueKind.I32,
                    importedGlobals,
                    references_,
                    11
                )
            )
            .ThrowsNothing();
    }

    [Test]
    public async Task I64の式にI32を要求する_指定セクションの終端位置で型不一致を報告する()
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithImmediate(new(0, 0x42), WasmValue.FromI64(0), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];

        // Act & Assert
        var exception = await Assert
            .That(() =>
                ConstantInitializer.Validate(expression, WasmValueKind.I32, [], references_, 11)
            )
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith("type mismatch", StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 42, null, 11));
        }
    }

    [Test]
    [Arguments((byte)0x6A)]
    [Arguments((byte)0x00)]
    public async Task 数値命令や制御命令を指定する_指定セクションの命令位置で定数式違反を報告する(
        byte opcode
    )
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithoutImmediate(new(0, opcode), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 41),
        ];

        // Act & Assert
        var exception = await Assert
            .That(() =>
                ConstantInitializer.Validate(expression, WasmValueKind.I32, [], references_, 11)
            )
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    exception!.Message.StartsWith(
                        "constant expression required",
                        StringComparison.Ordinal
                    )
                )
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, 40, null, 11));
        }
    }
}
