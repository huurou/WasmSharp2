using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("FB0003", 0, "unknown type 3: 参照する構造体型が存在しません")]
    [Arguments("FB00FFFFFFFF0F", 0, "unknown type 4294967295")]
    [Arguments("FB020200", 0, "non-structure type 2: 指定した型は構造体型ではありません")]
    [Arguments("FB020003", 0, "unknown field 3: 指定したフィールドが存在しません")]
    [Arguments("FB0200FFFFFFFF0F", 0, "unknown field 4294967295")]
    [Arguments("00FB018000", 1, "field type is not defaultable: 非nullの参照フィールドには初期値が必要")]
    [Arguments("00FB020001", 1, "field is packed: 詰め込みフィールドの読み取りには拡張方法が必要")]
    [Arguments("00FB030000", 1, "field is unpacked: 詰め込みフィールドだけを拡張")]
    [Arguments("00FB040002", 1, "field is unpacked")]
    [Arguments("FB050000", 0, "field is immutable: このフィールドは変更できません")]
    [Arguments("4100FB0000", 2, "type mismatch")] // 非nullの参照フィールドにi32を渡す。
    [Arguments("D001FB020000", 2, "type mismatch")] // 別の構造体型の参照から読み取る。
    [Arguments("D0004200FB050001", 4, "type mismatch")] // i8フィールドにi64を書き込む。
    public async Task 構造体命令の即値またはオペランドが不正なら_原因と命令の位置を返す(
        string body,
        int offset,
        string diagnostic
    )
    {
        // Arrange
        var instructions = Convert.FromHexString(body);
        var module = ReferenceControlModule.Create(
            [
                3,
                0x5F,
                3,
                0x7F,
                0,
                0x78,
                1,
                0x64,
                0x6F,
                1, // i32、可変i8、可変の非null外部参照
                0x5F,
                1,
                0x7E,
                1, // 可変i64を持つ別の構造体型
                0x60,
                0,
                0,
            ],
            instructions,
            typeIndex: 2
        );
        var start = module.InputLength - 1 - instructions.Length;

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(error!.Message).StartsWith(diagnostic);
            await Assert
                .That(error.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, start + offset, 0, 10));
        }
    }
}
