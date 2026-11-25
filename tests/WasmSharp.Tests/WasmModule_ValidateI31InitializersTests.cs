using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("FB1C", (byte)0x6C, "type mismatch", 3)] // ref.i31の入力がない
    [Arguments("4201FB1C", (byte)0x6C, "type mismatch", 3)] // 入力がi64
    [Arguments("4101FB1C", (byte)0x6F, "type mismatch", 1)] // 結果のi31をexternrefへ代入できない
    [Arguments("41014102FB1C", (byte)0x6C, "type mismatch", 1)] // i32とi31の2個が残る
    [Arguments("4101FB1CFB1E", (byte)0x7F, "constant expression required", 3)] // i31.get_uは定数式に使えない
    public async Task 定数式のi31命令や型が不正_違反した命令または結果の位置を報告する(
        string instructions,
        byte resultType,
        string expectedMessage,
        int offsetFromEnd
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Globals((resultType, false, [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(expectedMessage);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, binary.Length - offsetFromEnd, null, 6));
        }
    }
}
