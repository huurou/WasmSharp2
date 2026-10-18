using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    public async Task V128定数が関数の要求型と異なる_Decodeで即値を保持しValidateで終端位置を返す()
    {
        // Arrange
        var module = WasmModule.Decode(
            ConstantModuleBinary.Create(0x7F, [0xFD, 0x0C, .. new byte[16], 0x0B])
        );

        // Assert
        await Assert
            .That(module.Functions[0].Instructions[0].Immediate.AsV128())
            .IsEqualTo((0UL, 0UL));

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
                .IsEqualTo(new(WasmProcessingStage.Validate, 51, 0, 10));
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert
                .That(() => module.Instantiate([]))
                .ThrowsExactly<InvalidOperationException>();
        }
    }
}
