using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("FB007F", "unknown type 127", 4)]
    [Arguments("FB0103", "non-structure type 3", 4)]
    [Arguments("FB0102", "field type is not defaultable", 4)]
    [Arguments("FB0101FB0002", "type mismatch", 4)] // 先頭フィールドの値がない。
    [Arguments("FB0101FB0101FB0002", "type mismatch", 4)] // 先頭の参照先が別の具体型
    [Arguments("D000FB0101FB0002", "type mismatch", 4)] // 先頭のフィールドにnullは渡せない。
    [Arguments("FB0101", "type mismatch", 1)] // globalの結果型は(ref 0)
    [Arguments("FB0000FB020000", "constant expression required", 5)]
    [Arguments("FB0000FB030000", "constant expression required", 5)]
    [Arguments("FB0000FB040000", "constant expression required", 5)]
    [Arguments("FB0000FB050000", "constant expression required", 5)]
    [Arguments("FB00002300", "unknown global 0", 3)]
    public async Task 構造体の定数式が不正なら_適格性を先に確認し違反した命令または終端の位置を返す(
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        byte[] first = [0x5F, 1, 0x7F, 0]; // (struct (field i32))
        byte[] second = [0x5F, 1, 0x7E, 0]; // (struct (field i64))
        byte[] pair = [0x5F, 2, 0x64, 0, 0, 0x64, 1, 0]; // (struct (field (ref 0)) (field (ref 1)))
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [4, .. first, .. second, .. pair, 0x60, 0, 0]),
            HostLinkingModuleBinary.Section(6, [1, 0x64, 0, 0, .. Convert.FromHexString(instructions), 0x0B])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith(diagnostic);
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, binary.Length - offsetFromEnd, null, 6));
        }
    }
}
