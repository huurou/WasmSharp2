using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments(0x6Bu)]
    [Arguments(0x8Bu)]
    [Arguments(0xABu)]
    [Arguments(0xCBu)]
    public async Task SIMD整数接続のshiftにI32とV128を逆順で積む_Validateで命令位置の型不一致を返す(
        uint opcode
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F, 0x7B], [0x7B])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Code(
                ([], [0x20, 0, 0x20, 1, 0xFD, .. HostLinkingModuleBinary.Unsigned(opcode), 0x0B])
            )
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        WasmModule[] modules = [WasmModule.Decode(bytes), WasmModule.Decode(stream)];

        foreach (var module in modules)
        {
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
                        new(WasmProcessingStage.Validate, Array.IndexOf(bytes, (byte)0xFD), 0, 10)
                    );
                await Assert.That(module.FunctionCodes.IsEmpty).IsTrue();
                await Assert
                    .That(() => module.Instantiate([]))
                    .ThrowsExactly<InvalidOperationException>();
            }
        }
    }
}
