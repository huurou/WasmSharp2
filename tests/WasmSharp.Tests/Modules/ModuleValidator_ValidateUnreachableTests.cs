using WasmSharp.Exceptions;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal partial class ModuleValidator_ValidateTests
{
    [Test]
    [Arguments("6000017F", "00001A0B")]
    [Arguments("6000017F", "0041010F1A0B")]
    [Arguments("60017F017F", "000021000B")]
    [Arguments("60017F017F", "000022000B")]
    [Arguments("60017F017F", "000010000B")]
    [Arguments("6000027F7E", "00000F0B")]
    [Arguments("6000027F7E", "000042010B")]
    [Arguments("6000017F", "004201000B")]
    [Arguments("6000017F", "00420141010F0B")]
    [Arguments("6000017F", "00004101000B")]
    public async Task 到達不能部分で底から値を取り出す_型多相性で成立する命令列を受理する(
        string signature,
        string body
    )
    {
        // Arrange
        var module = DecodeFunction(signature, body);

        // Act
        var result = module.Validate();

        // Assert
        await Assert.That(result).IsSameReferenceAs(module);
        await Assert.That(module.FunctionCodes.Length).IsEqualTo(1);
    }

    [Test]
    [Arguments("6000017F", "000042010B", 2)]
    [Arguments("6000017F", "0000410141020B", 3)]
    [Arguments("6000017F", "000042010F0B", 2)]
    [Arguments("60017F017F", "0000420121000B", 2)]
    [Arguments("60017F017F", "0000420122000B", 2)]
    [Arguments("60017F017F", "0000420110000B", 2)]
    [Arguments("60017E017F", "000022000B", 2)]
    [Arguments("6000017F", "0041010F20000B", 2)]
    [Arguments("6000017F", "000021000B", 1)]
    [Arguments("6000017F", "000022FFFFFFFF0F0B", 1)]
    [Arguments("6000017F", "000010010B", 1)]
    public async Task 到達不能部分に具体型の不一致や不正添字や余剰値がある_命令位置付きで拒否する(
        string signature,
        string body,
        int instructionIndex
    )
    {
        // Arrange
        var module = DecodeFunction(signature, body);

        // Act & Assert
        var exception = await Assert
            .That(() => module.Validate())
            .ThrowsExactly<WasmValidateException>();
        await Assert
            .That(exception!.Location)
            .IsEqualTo(
                new(
                    WasmProcessingStage.Validate,
                    module.Functions[0].Instructions[instructionIndex].ByteOffset,
                    0,
                    10
                )
            );
    }

    [Test]
    [Arguments("0024010B", -1)]
    [Arguments("00410124010B", -1)]
    [Arguments("0024000B", 1)]
    [Arguments("00420124010B", 2)]
    [Arguments("0024020B", 1)]
    [Arguments("0023021A0B", 1)]
    public async Task 到達不能部分のglobal操作を検証する_多相入力を許し可変性と添字と具体型を検査する(
        string body,
        int failureInstruction
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Globals((0x7F, true, [0x41, 0, 0x0B])),
                HostLinkingModuleBinary.Code(([], Convert.FromHexString(body)))
            )
        );

        // Act & Assert
        if (failureInstruction < 0)
        {
            await Assert.That(() => module.Validate()).ThrowsNothing();
        }
        else
        {
            var exception = await Assert
                .That(() => module.Validate())
                .ThrowsExactly<WasmValidateException>();
            await Assert
                .That(exception!.Location)
                .IsEqualTo(
                    new(
                        WasmProcessingStage.Validate,
                        module.Functions[0].Instructions[failureInstruction].ByteOffset,
                        0,
                        10
                    )
                );
        }
    }
}
