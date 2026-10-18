using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("1101010B", (byte)0x70, "unknown table 1", 0)]
    [Arguments("1101000B", (byte)0x6F, "unknown type 1", 0)]
    [Arguments("1100000B", (byte)0x6F, "type mismatch", 0)]
    [Arguments("001101010B", (byte)0x70, "unknown table 1", 1)]
    [Arguments("001101000B", (byte)0x6F, "unknown type 1", 1)]
    [Arguments("001100000B", (byte)0x6F, "type mismatch", 1)]
    [Arguments("1100000B", (byte)0x70, "type mismatch", 0)]
    [Arguments("42001100000B", (byte)0x70, "type mismatch", 1)]
    public async Task 間接callに複合不正がある_到達不能でもtableと型とfuncref制約をoperandより先に検証する(
        string body,
        byte tableType,
        string prefix,
        int instructionIndex
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Tables((tableType, 0, null)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert
                .That(exception!.Message.StartsWith(prefix, StringComparison.Ordinal))
                .IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[instructionIndex].ByteOffset,
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
    [Arguments("001100000B")]
    [Arguments("4100410041001100000B")]
    [Arguments("4200410041001100000B")]
    public async Task 間接callの引数型または順序が不正_到達不能のUnknownだけを許容する(string body)
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([0x7F, 0x7E], []), ([], [])),
                HostLinkingModuleBinary.Functions(1),
                HostLinkingModuleBinary.Tables((0x70, 0, null)),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        if (body.StartsWith("00", StringComparison.Ordinal))
        {
            // Act
            var validated = module.Validate();

            // Assert
            await Assert.That(validated).IsSameReferenceAs(module);
        }
        else
        {
            // Act & Assert
            await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        }
    }
}
