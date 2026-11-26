using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("FB00FFFFFFFF0F", "unknown type 4294967295")]
    [Arguments("FB020200", "non-structure type 2")]
    [Arguments("FB0200FFFFFFFF0F", "unknown field 4294967295")]
    [Arguments("00FB018000", "field type is not defaultable")]
    [Arguments("00FB020001", "field is packed")]
    [Arguments("00FB030000", "field is unpacked")]
    [Arguments("00FB040002", "field is unpacked")]
    [Arguments("FB050000", "field is immutable")]
    [Arguments("4100FB0000", "type mismatch")] // 非nullの参照フィールドにi32を渡す。
    [Arguments("D001FB020000", "type mismatch")] // 別の構造体型の参照から読み取る。
    [Arguments("D0004200FB050001", "type mismatch")] // i8フィールドにi64を書き込む。
    public async Task 構造体命令の即値またはオペランドが不正なら_原因に対応する検証診断を返す(string body, string diagnostic)
    {
        // Arrange
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
            Convert.FromHexString(body),
            typeIndex: 2
        );

        // Act & Assert
        var error = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(error!.Message).StartsWith(diagnostic);
    }
}
