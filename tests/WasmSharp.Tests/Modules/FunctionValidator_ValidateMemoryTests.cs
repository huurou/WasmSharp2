using WasmSharp.Instructions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class FunctionValidator_ValidateTests
{
    [Test]
    public async Task ロードとストアを線形化する_専用即値と宣言のアクセス情報と元位置を保持しmoduleを変更しない()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Memories((1, null)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString("410141012802023602030B")))
            )
        );
        var decoded = module.Functions[0].Instructions;

        // Act
        var code = FunctionValidator.Validate(
            module,
            0,
            0,
            [module.Types[0]],
            [],
            ReferenceValidationContext.Create(module, 0)
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(code.Instructions[2].MemoryArgument)
                .IsEqualTo(new MemoryArgument(2, 2));
            await Assert
                .That(code.Instructions[3].MemoryArgument)
                .IsEqualTo(new MemoryArgument(2, 3));
            await Assert
                .That(code.Instructions[2].MemoryAccessSignature)
                .IsEqualTo(new MemoryAccessSignature(WasmValueKind.I32, 4, false));
            await Assert
                .That(code.Instructions[3].MemoryAccessSignature)
                .IsEqualTo(new MemoryAccessSignature(WasmValueKind.I32, 4, false));
            await Assert.That(code.Instructions[2].ByteOffset).IsEqualTo(decoded[2].ByteOffset);
            await Assert.That(code.Instructions[3].ByteOffset).IsEqualTo(decoded[3].ByteOffset);
            await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
            await Assert.That(module.FunctionExportIndices.IsEmpty).IsTrue();
        }
    }
}
