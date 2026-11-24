using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("6300", "41002000", "2600")] // table.set
    [Arguments("6401", "41002000", "2600")]
    [Arguments("6300", "20004100", "FC0F001A")] // table.grow、drop
    [Arguments("6401", "20004100", "FC0F001A")]
    [Arguments("6300", "410020004100", "FC1100")] // table.fill
    [Arguments("6401", "410020004100", "FC1100")]
    public async Task 非nullの型付きテーブルにnullを許す参照か別型の関数参照を書き込む_対象命令の位置で型不一致を報告する(
        string parameterType,
        string operands,
        string operation
    )
    {
        // Arrange
        // テーブルの要素は(ref 0)で、書き込む引数は(ref null 0)か(ref 1)になる。
        byte[] emptyFunction = [0x60, 0, 0];
        byte[] otherFunction = [0x60, 1, 0x7F, 0];
        byte[] writer = [0x60, 1, .. Convert.FromHexString(parameterType), 0];
        var operationBytes = Convert.FromHexString(operation);
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [3, .. emptyFunction, .. otherFunction, .. writer]),
            HostLinkingModuleBinary.Imports(("env", "t", 1, [0x64, 0, 0, 0])),
            HostLinkingModuleBinary.Functions(0, 2),
            HostLinkingModuleBinary.Code(([], [0x0B]), ([], [.. Convert.FromHexString(operands), .. operationBytes, 0x0B]))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);
        var operationOffset = binary.Length - operationBytes.Length - 1;

        // Act & Assert
        var failure = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(failure!.Message).StartsWith("type mismatch");
            await Assert
                .That(failure.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, operationOffset, 1, 10));
        }
    }
}
