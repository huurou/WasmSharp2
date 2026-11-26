using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(true, (byte)2)]
    [Arguments(false, (byte)10)]
    public async Task 関数の宣言に構造体型を指定する_検証で関数型を要求する診断を返す(bool imported, byte expectedSection)
    {
        // Arrange
        var types = HostLinkingModuleBinary.Section(1, [2, 0x60, 0, 0, 0x5F, 0]);
        var binary = imported
            ? HostLinkingModuleBinary.Create(types, HostLinkingModuleBinary.Imports(("host", "f", 0, [1])))
            : HostLinkingModuleBinary.Create(
                types,
                HostLinkingModuleBinary.Functions(1),
                HostLinkingModuleBinary.Code(([], [0x0B]))
            );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith("non-function type 1");
            await Assert.That(error.Message).Contains("関数型ではありません");
            await Assert.That(error.Location?.FunctionIndex).IsEqualTo(0u);
            await Assert.That(error.Location?.SectionId).IsEqualTo(expectedSection);
        }
    }

    [Test]
    [Arguments("02010B", "non-function type 1")] // block
    [Arguments("1401", "non-function type 1")] // call_ref
    [Arguments("110100", "non-function type 1")] // call_indirectではテーブルの要素型より先に関数型を検査する。
    [Arguments("110101", "unknown table 1")] // 存在しないテーブルは関数型より先に報告する。
    public async Task 関数型を要求する命令に構造体型を指定する_命令位置と最初の原因を返す(string body, string diagnostic)
    {
        // Arrange
        var instructions = Convert.FromHexString(body);
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [2, 0x60, 0, 0, 0x5F, 0]),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Tables((0x6F, 0, null)),
            HostLinkingModuleBinary.Code(([], [.. instructions, 0x0B]))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);
        var offset = binary.Length - instructions.Length - 1;

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith(diagnostic);
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, offset, 0, 10));
        }
    }
}
